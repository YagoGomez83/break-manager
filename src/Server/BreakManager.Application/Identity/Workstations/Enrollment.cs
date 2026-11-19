using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Presence;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using Microsoft.Extensions.Options;

namespace BreakManager.Application.Identity.Workstations;

/// <summary>Formato canónico del código de enrolamiento: mayúsculas, sin espacios ni guiones.</summary>
public static class EnrollmentCodeFormat
{
    public static string Normalize(string? code) =>
        new((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());
}

public sealed record GenerateEnrollmentCodeCommand(Guid WorkstationId, Guid ActorUserId);

/// <summary>El código en claro se muestra una sola vez.</summary>
public sealed record GeneratedEnrollmentCode(string Code, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"GeneratedEnrollmentCode {{ ExpiresAt = {ExpiresAt:O} }}";
}

internal sealed class GenerateEnrollmentCodeHandler(
    IWorkstationRepository workstations,
    IEnrollmentCodeRepository codes,
    ITokenService tokens,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<IdentityParameters> parameters) : ICommandHandler<GenerateEnrollmentCodeCommand, Result<GeneratedEnrollmentCode>>
{
    public async Task<Result<GeneratedEnrollmentCode>> HandleAsync(
        GenerateEnrollmentCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var workstation = await workstations.GetByIdAsync(command.WorkstationId, cancellationToken).ConfigureAwait(false);
        if (workstation is null)
        {
            return IdentityErrors.WorkstationNotFound;
        }

        var now = time.GetUtcNow();
        foreach (var pending in await codes.ListPendingForWorkstationAsync(workstation.Id, cancellationToken).ConfigureAwait(false))
        {
            pending.Invalidate(now);
        }

        var code = EnrollmentCodeFormat.Normalize(tokens.GenerateEnrollmentCode());
        var enrollmentCode = EnrollmentCode.Issue(
            Guid.CreateVersion7(now), workstation.Id, tokens.Hash(code), now, parameters.Value.EnrollmentCodeLifetime);
        codes.Add(enrollmentCode);

        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.EnrollmentCodeGenerated,
            now,
            SecurityActorType.User,
            command.ActorUserId,
            workstation.Id));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new GeneratedEnrollmentCode(code, enrollmentCode.ExpiresAt);
    }
}

public sealed record RedeemEnrollmentCodeCommand(string Code, string ClientIp)
{
    public override string ToString() => $"RedeemEnrollmentCodeCommand {{ ClientIp = {ClientIp} }}";
}

/// <summary>El token de dispositivo en claro se entrega una sola vez.</summary>
public sealed record EnrolledDevice(Guid WorkstationId, int WorkstationNumber, string DeviceToken)
{
    public override string ToString() => $"EnrolledDevice {{ WorkstationId = {WorkstationId}, WorkstationNumber = {WorkstationNumber} }}";
}

internal sealed class RedeemEnrollmentCodeHandler(
    IWorkstationRepository workstations,
    IEnrollmentCodeRepository codes,
    ITokenService tokens,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<RedeemEnrollmentCodeCommand, Result<EnrolledDevice>>
{
    public async Task<Result<EnrolledDevice>> HandleAsync(RedeemEnrollmentCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalized = EnrollmentCodeFormat.Normalize(command.Code);
        if (normalized.Length == 0)
        {
            return IdentityErrors.InvalidEnrollmentCode;
        }

        var code = await codes.GetByHashAsync(tokens.Hash(normalized), cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        if (code is null || code.Redeem(now).IsFailure)
        {
            return IdentityErrors.InvalidEnrollmentCode;
        }

        var workstation = await workstations.GetByIdAsync(code.WorkstationId, cancellationToken).ConfigureAwait(false);
        if (workstation is null)
        {
            return IdentityErrors.InvalidEnrollmentCode;
        }

        var deviceToken = tokens.GenerateToken();
        var credential = workstation.Enroll(Guid.CreateVersion7(now), tokens.Hash(deviceToken), now);
        credential.RecordSeen(now, command.ClientIp);

        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.WorkstationEnrolled,
            now,
            SecurityActorType.Device,
            credential.Id,
            workstation.Id,
            new Dictionary<string, string> { ["ip"] = command.ClientIp }));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new EnrolledDevice(workstation.Id, workstation.Number, deviceToken);
    }
}

public sealed record RevokeWorkstationCommand(Guid WorkstationId, Guid ActorUserId);

internal sealed class RevokeWorkstationHandler(
    IWorkstationRepository workstations,
    IWorkstationSessionService sessions,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<RevokeWorkstationCommand, Result>
{
    public async Task<Result> HandleAsync(RevokeWorkstationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var workstation = await workstations.GetByIdAsync(command.WorkstationId, cancellationToken).ConfigureAwait(false);
        if (workstation is null)
        {
            return Result.Failure(IdentityErrors.WorkstationNotFound);
        }

        var now = time.GetUtcNow();
        workstation.Revoke(now);
        await sessions.CloseOpenForWorkstationAsync(workstation.Id, SessionEndReason.Revocation, cancellationToken)
            .ConfigureAwait(false);

        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.WorkstationRevoked,
            now,
            SecurityActorType.User,
            command.ActorUserId,
            workstation.Id));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
