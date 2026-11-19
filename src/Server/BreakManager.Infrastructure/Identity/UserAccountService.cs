using BreakManager.Application.Identity;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BreakManager.Infrastructure.Identity;

/// <summary>
/// Cuentas web sobre ASP.NET Core Identity (ADR-0011). Cada cambio que debe cortar la sesión
/// (desactivar, cambiar el rol, resetear la contraseña) actualiza el security stamp.
/// </summary>
internal sealed class UserAccountService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    BreakManagerDbContext dbContext,
    TimeProvider time) : IUserAccountService
{
    public async Task<Result<Guid>> CreateAsync(NewUserAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await EnsureRoleAsync(account.Role).ConfigureAwait(false);

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            UserName = account.UserName,
            Group = account.Group,
            MustChangePassword = true,
            LockoutEnabled = true,
        };

        var created = await userManager.CreateAsync(user, account.TemporaryPassword).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return ToError(created);
        }

        var inRole = await userManager.AddToRoleAsync(user, account.Role.ToString()).ConfigureAwait(false);
        return inRole.Succeeded ? user.Id : ToError(inRole);
    }

    public async Task<Result> UpdateRoleAsync(Guid userId, WebRole role, Group? group, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        await EnsureRoleAsync(role).ConfigureAwait(false);

        var current = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var removed = await userManager.RemoveFromRolesAsync(user, current).ConfigureAwait(false);
        if (!removed.Succeeded)
        {
            return Result.Failure(ToError(removed));
        }

        var added = await userManager.AddToRoleAsync(user, role.ToString()).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            return Result.Failure(ToError(added));
        }

        user.Group = group;
        return await SaveWithNewStampAsync(user).ConfigureAwait(false);
    }

    public async Task<Result> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        user.Active = active;
        return await SaveWithNewStampAsync(user).ConfigureAwait(false);
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        // Validar antes de quitar la contraseña actual, para no dejar al usuario sin contraseña.
        foreach (var validator in userManager.PasswordValidators)
        {
            var valid = await validator.ValidateAsync(userManager, user, temporaryPassword).ConfigureAwait(false);
            if (!valid.Succeeded)
            {
                return Result.Failure(ToError(valid));
            }
        }

        var removed = await userManager.RemovePasswordAsync(user).ConfigureAwait(false);
        var reset = removed.Succeeded
            ? await userManager.AddPasswordAsync(user, temporaryPassword).ConfigureAwait(false)
            : removed;
        if (!reset.Succeeded)
        {
            return Result.Failure(ToError(reset));
        }

        user.MustChangePassword = true;
        await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        return await SaveWithNewStampAsync(user).ConfigureAwait(false);
    }

    public async Task<UserAccountSummary?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        (await QuerySummaries().Where(u => u.Id == userId).ToListAsync(cancellationToken).ConfigureAwait(false))
        .Select(ToSummary)
        .SingleOrDefault();

    public async Task<IReadOnlyList<UserAccountSummary>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await QuerySummaries().OrderBy(u => u.UserName).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(ToSummary)];

    private IQueryable<UserRow> QuerySummaries() =>
        from user in dbContext.Users.AsNoTracking()
        join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
        join role in dbContext.Roles on userRole.RoleId equals role.Id
        select new UserRow(user.Id, user.UserName!, role.Name!, user.Group, user.Active, user.LockoutEnd);

    private UserAccountSummary ToSummary(UserRow row) =>
        new(
            row.Id,
            row.UserName,
            Enum.Parse<WebRole>(row.RoleName),
            row.Group,
            row.Active,
            LockedOut: row.LockoutEnd > time.GetUtcNow());

    private async Task EnsureRoleAsync(WebRole role)
    {
        var name = role.ToString();
        if (!await roleManager.RoleExistsAsync(name).ConfigureAwait(false))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(name) { Id = Guid.CreateVersion7(time.GetUtcNow()) }).ConfigureAwait(false);
        }
    }

    private async Task<Result> SaveWithNewStampAsync(ApplicationUser user)
    {
        var updated = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updated.Succeeded)
        {
            return Result.Failure(ToError(updated));
        }

        var stamped = await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        return stamped.Succeeded ? Result.Success() : Result.Failure(ToError(stamped));
    }

    private static Error ToError(IdentityResult result) =>
        new(
            "Identity." + (result.Errors.FirstOrDefault()?.Code ?? "Failed"),
            string.Join(" ", result.Errors.Select(e => e.Description)));

    private sealed record UserRow(Guid Id, string UserName, string RoleName, Group? Group, bool Active, DateTimeOffset? LockoutEnd);
}
