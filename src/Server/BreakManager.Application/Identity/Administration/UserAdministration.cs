using BreakManager.Application.Abstractions;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity.Administration;

/// <summary>Alta de usuario web con contraseña temporal (HU-05). La política de contraseñas la aplica Identity.</summary>
public sealed record CreateUserCommand(string UserName, WebRole Role, Group? Group, string TemporaryPassword)
{
    public override string ToString() => $"CreateUserCommand {{ Role = {Role}, Group = {Group} }}";
}

internal sealed class CreateUserHandler(IUserAccountService users) : ICommandHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var roleCheck = UserRules.CheckGroup(command.Role, command.Group);
        if (roleCheck.IsFailure)
        {
            return roleCheck.Error;
        }

        return await users
            .CreateAsync(new NewUserAccount(command.UserName.Trim(), command.Role, command.Group, command.TemporaryPassword), cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record UpdateUserRoleCommand(Guid UserId, WebRole Role, Group? Group);

internal sealed class UpdateUserRoleHandler(IUserAccountService users) : ICommandHandler<UpdateUserRoleCommand, Result>
{
    public async Task<Result> HandleAsync(UpdateUserRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var roleCheck = UserRules.CheckGroup(command.Role, command.Group);
        return roleCheck.IsFailure
            ? roleCheck
            : await users.UpdateRoleAsync(command.UserId, command.Role, command.Group, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record SetUserActiveCommand(Guid UserId, bool Active, Guid ActorUserId);

internal sealed class SetUserActiveHandler(IUserAccountService users) : ICommandHandler<SetUserActiveCommand, Result>
{
    public async Task<Result> HandleAsync(SetUserActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.Active && command.UserId == command.ActorUserId)
        {
            return Result.Failure(IdentityErrors.CannotDeactivateSelf);
        }

        return await users.SetActiveAsync(command.UserId, command.Active, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ResetUserPasswordCommand(Guid UserId, string TemporaryPassword)
{
    public override string ToString() => $"ResetUserPasswordCommand {{ UserId = {UserId} }}";
}

internal sealed class ResetUserPasswordHandler(IUserAccountService users) : ICommandHandler<ResetUserPasswordCommand, Result>
{
    public Task<Result> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return users.ResetPasswordAsync(command.UserId, command.TemporaryPassword, cancellationToken);
    }
}

internal static class UserRules
{
    /// <summary>Solo el supervisor tiene grupo, y siempre lo tiene (RN-33).</summary>
    public static Result CheckGroup(WebRole role, Group? group) =>
        (role == WebRole.Supervisor) == group.HasValue
            ? Result.Success()
            : Result.Failure(IdentityErrors.SupervisorGroupRequired);
}
