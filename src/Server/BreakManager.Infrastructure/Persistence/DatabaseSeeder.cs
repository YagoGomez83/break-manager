using BreakManager.Application.Identity;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Infrastructure.Persistence;

/// <summary>Datos de arranque que pasa el comando <c>migrate</c>.</summary>
/// <param name="AdminUserName">BOOTSTRAP_ADMIN_USERNAME (opcional).</param>
/// <param name="AdminPassword">BOOTSTRAP_ADMIN_PASSWORD (opcional; se borra del .env después del primer ingreso).</param>
/// <param name="IsDevelopment">Solo en Development se publica el aviso BORRADOR.</param>
public sealed record SeedSettings(string? AdminUserName, string? AdminPassword, bool IsDevelopment)
{
    public override string ToString() => $"SeedSettings {{ AdminUserName set = {AdminUserName is not null}, IsDevelopment = {IsDevelopment} }}";
}

/// <summary>Qué se sembró (para el log del comando <c>migrate</c>; sin datos personales).</summary>
public sealed record SeedReport(bool AdminCreated, Guid? AdminId, bool DraftNoticePublished);

internal static class DatabaseSeeder
{
    /// <summary>
    /// Texto de prueba, solo para Development. El aviso real lo redacta y publica la organización (Q10).
    /// </summary>
    internal const string DraftNoticeText =
        "BORRADOR — AVISO DE PRUEBA PARA DESARROLLO. NO USAR EN PRODUCCIÓN.\n\n"
        + "Este puesto registra el inicio y el fin de la sesión del operador, los descansos y los períodos sin actividad. "
        + "No se registran teclas, pantallas ni contenido. El texto definitivo lo redacta y aprueba la organización.";

    public static async Task<Result<SeedReport>> SeedAsync(IServiceProvider services, SeedSettings settings, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await EnsureRolesAsync(provider).ConfigureAwait(false);

        var admin = await SeedAdminAsync(provider, settings, cancellationToken).ConfigureAwait(false);
        if (admin.IsFailure)
        {
            return admin.Error;
        }

        var noticePublished = settings.IsDevelopment
            && await SeedDraftNoticeAsync(provider, admin.Value, cancellationToken).ConfigureAwait(false);

        return new SeedReport(admin.Value is not null, admin.Value, noticePublished);
    }

    private static async Task EnsureRolesAsync(IServiceProvider provider)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var time = provider.GetRequiredService<TimeProvider>();

        foreach (var role in Enum.GetNames<WebRole>())
        {
            if (!await roles.RoleExistsAsync(role).ConfigureAwait(false))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7(time.GetUtcNow()) }).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Result<Guid?>> SeedAdminAsync(IServiceProvider provider, SeedSettings settings, CancellationToken cancellationToken)
    {
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await users.GetUsersInRoleAsync(nameof(WebRole.Administrator)).ConfigureAwait(false)).Count > 0)
        {
            return Result.Success<Guid?>(null);
        }

        if (string.IsNullOrWhiteSpace(settings.AdminUserName) || string.IsNullOrEmpty(settings.AdminPassword))
        {
            return Result.Success<Guid?>(null);
        }

        var created = await provider.GetRequiredService<IUserAccountService>()
            .CreateAsync(new NewUserAccount(settings.AdminUserName.Trim(), WebRole.Administrator, null, settings.AdminPassword), cancellationToken)
            .ConfigureAwait(false);

        return created.IsSuccess ? Result.Success<Guid?>(created.Value) : created.Error;
    }

    private static async Task<bool> SeedDraftNoticeAsync(IServiceProvider provider, Guid? adminId, CancellationToken cancellationToken)
    {
        var dbContext = provider.GetRequiredService<BreakManagerDbContext>();
        if (await dbContext.Set<PrivacyNotice>().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var now = provider.GetRequiredService<TimeProvider>().GetUtcNow();
        var notice = PrivacyNotice.Publish(Guid.CreateVersion7(now), null, DraftNoticeText, now, adminId ?? Guid.Empty).Value;
        dbContext.Add(notice);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
