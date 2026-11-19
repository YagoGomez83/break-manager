using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Presence;
using BreakManager.Domain.Common;

namespace BreakManager.Application.Identity.Workstations;

/// <summary>
/// Autentica un request del agente: token de dispositivo y, opcionalmente, token de sesión (ADR-0010).
/// Registra el último uso y el cambio de IP del token.
/// </summary>
public sealed record AuthenticateDeviceCommand(string DeviceToken, string? SessionToken, string ClientIp)
{
    public override string ToString() => $"AuthenticateDeviceCommand {{ ClientIp = {ClientIp} }}";
}

public sealed record AuthenticatedDevice(Guid WorkstationId, Guid CredentialId, int WorkstationNumber, SessionInfo? Session);

internal sealed class AuthenticateDeviceHandler(
    IWorkstationRepository workstations,
    IWorkstationSessionService sessions,
    ITokenService tokens,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<AuthenticateDeviceCommand, Result<AuthenticatedDevice>>
{
    public async Task<Result<AuthenticatedDevice>> HandleAsync(AuthenticateDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = time.GetUtcNow();
        var tokenHash = tokens.Hash(command.DeviceToken);
        var workstation = await workstations.GetByCredentialTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);
        var credential = workstation?.ActiveCredential;

        if (workstation is null || workstation.Revoked || credential is null || credential.TokenHash != tokenHash)
        {
            securityEvents.Record(new SecurityEvent(
                Guid.CreateVersion7(now),
                SecurityEventType.InvalidDeviceToken,
                now,
                SecurityActorType.Device,
                ActorId: null,
                workstation?.Id,
                new Dictionary<string, string> { ["ip"] = command.ClientIp }));
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IdentityErrors.InvalidDeviceToken;
        }

        var seen = credential.RecordSeen(now, command.ClientIp);
        if (seen.IpChanged)
        {
            securityEvents.Record(new SecurityEvent(
                Guid.CreateVersion7(now),
                SecurityEventType.DeviceTokenIpChanged,
                now,
                SecurityActorType.Device,
                credential.Id,
                workstation.Id,
                new Dictionary<string, string> { ["previousIp"] = seen.PreviousIp!, ["ip"] = command.ClientIp }));
        }

        if (seen.Modified)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        SessionInfo? session = null;
        if (!string.IsNullOrWhiteSpace(command.SessionToken))
        {
            session = await sessions.FindUsableByTokenAsync(command.SessionToken, cancellationToken).ConfigureAwait(false);
            if (session is null || session.WorkstationId != workstation.Id)
            {
                return IdentityErrors.InvalidSession;
            }
        }

        return new AuthenticatedDevice(workstation.Id, credential.Id, workstation.Number, session);
    }
}
