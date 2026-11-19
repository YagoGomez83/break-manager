using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity;

/// <summary>
/// Cuentas de usuario web (ADR-0011): se implementan con ASP.NET Core Identity en Infrastructure.
/// Desactivar, cambiar el rol o resetear la contraseña actualiza el security stamp (la sesión se corta al revalidar).
/// </summary>
public interface IUserAccountService
{
    Task<Result<Guid>> CreateAsync(NewUserAccount account, CancellationToken cancellationToken);

    Task<Result> UpdateRoleAsync(Guid userId, WebRole role, Group? group, CancellationToken cancellationToken);

    Task<Result> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken);

    /// <summary>Asigna una contraseña temporal: el usuario debe cambiarla en el próximo ingreso.</summary>
    Task<Result> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken);

    Task<UserAccountSummary?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccountSummary>> ListAsync(CancellationToken cancellationToken);
}

public sealed record NewUserAccount(string UserName, WebRole Role, Group? Group, string TemporaryPassword);

public sealed record UserAccountSummary(Guid Id, string UserName, WebRole Role, Group? Group, bool Active, bool LockedOut);

/// <summary>Roles web (01-requisitos.md §1). Los operadores no tienen cuenta web.</summary>
public enum WebRole
{
    Administrator,
    Coordinator,
    Quality,
    Supervisor,
}
