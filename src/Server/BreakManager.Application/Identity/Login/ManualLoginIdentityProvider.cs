using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using Microsoft.Extensions.Options;

namespace BreakManager.Application.Identity.Login;

/// <summary>
/// Identifica al operador que se loguea en un puesto (ADR-0005). Hoy, solo por legajo + PIN;
/// más adelante podrá haber una implementación basada en Milestone.
/// </summary>
public interface IOperatorIdentityProvider
{
    /// <summary>Devuelve el ID del operador, o un error genérico. Registra y guarda los intentos fallidos.</summary>
    Task<Result<Guid>> AuthenticateAsync(OperatorCredentials credentials, Guid workstationId, CancellationToken cancellationToken);
}

public sealed record OperatorCredentials(string EmployeeNumber, string Pin)
{
    public override string ToString() => "OperatorCredentials { *** }";
}

/// <summary>
/// Login manual con legajo + PIN y bloqueo progresivo (HU-01, R20).
/// </summary>
internal sealed class ManualLoginIdentityProvider(
    IOperatorRepository operators,
    IPinHasher pinHasher,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<IdentityParameters> parameters) : IOperatorIdentityProvider
{
    public async Task<Result<Guid>> AuthenticateAsync(
        OperatorCredentials credentials,
        Guid workstationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var employeeNumber = EmployeeNumber.Create(credentials.EmployeeNumber);
        if (employeeNumber.IsFailure)
        {
            return IdentityErrors.InvalidCredentials;
        }

        var op = await operators.GetByEmployeeNumberAsync(employeeNumber.Value, cancellationToken).ConfigureAwait(false);
        if (op is null)
        {
            return IdentityErrors.InvalidCredentials;
        }

        var now = time.GetUtcNow();
        var wasLockedOut = op.IsLockedOut(now);
        var attempt = op.AttemptLogin(credentials.Pin, now, parameters.Value.ToOperatorLockoutPolicy(), pinHasher);

        switch (attempt)
        {
            case LoginAttemptResult.Succeeded:
                return op.Id;

            case LoginAttemptResult.InvalidCredentials:
                if (op.Active)
                {
                    RecordFailure(op.Id, workstationId, now);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return IdentityErrors.InvalidCredentials;

            case LoginAttemptResult.LockedOut when wasLockedOut:
                return IdentityErrors.OperatorLockedOut;

            case LoginAttemptResult.LockedOut:
            case LoginAttemptResult.LockedOutUntilManualUnlock:
                RecordFailure(op.Id, workstationId, now);
                securityEvents.Record(new SecurityEvent(
                    Guid.CreateVersion7(now),
                    SecurityEventType.OperatorLockedOut,
                    now,
                    SecurityActorType.Operator,
                    op.Id,
                    workstationId,
                    new Dictionary<string, string>
                    {
                        ["requiresManualUnlock"] = attempt == LoginAttemptResult.LockedOutUntilManualUnlock ? "true" : "false",
                    }));
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return IdentityErrors.OperatorLockedOut;

            default:
                throw new InvalidOperationException($"Resultado de login inesperado: {attempt}.");
        }
    }

    private void RecordFailure(Guid operatorId, Guid workstationId, DateTimeOffset now) =>
        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.OperatorLoginFailed,
            now,
            SecurityActorType.Operator,
            operatorId,
            workstationId));
}
