using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Ticket de inicio de sesión: une el login con los pasos pendientes (aceptar el aviso, cambiar el PIN).
/// Ligado al operador, al token de dispositivo y a la versión del aviso; de un solo uso (ADR-0010).
/// Se consume en la misma transacción que abre la sesión.
/// </summary>
public sealed class LoginTicket : AggregateRoot<Guid>
{
    private LoginTicket(
        Guid id,
        Guid operatorId,
        Guid deviceCredentialId,
        string tokenHash,
        int? noticeVersion,
        bool requiresPinChange,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        OperatorId = operatorId;
        DeviceCredentialId = deviceCredentialId;
        TokenHash = tokenHash;
        NoticeVersion = noticeVersion;
        RequiresPinChange = requiresPinChange;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid OperatorId { get; }

    public Guid DeviceCredentialId { get; }

    public string TokenHash { get; }

    /// <summary>Versión del aviso que el operador debe aceptar; <c>null</c> si no hace falta.</summary>
    public int? NoticeVersion { get; }

    public bool RequiresPinChange { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static LoginTicket Issue(
        Guid id,
        Guid operatorId,
        Guid deviceCredentialId,
        string tokenHash,
        int? noticeVersion,
        bool requiresPinChange,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new LoginTicket(
            id, operatorId, deviceCredentialId, tokenHash, noticeVersion, requiresPinChange, now, now + lifetime);
    }

    /// <summary>
    /// Consume el ticket. Falla, sin consumirlo, si viene de otro dispositivo, si ya se usó, si venció
    /// o si no se aceptó la versión del aviso pedida.
    /// </summary>
    public Result Consume(DateTimeOffset now, Guid deviceCredentialId, int? acceptedNoticeVersion)
    {
        if (deviceCredentialId != DeviceCredentialId)
        {
            return Result.Failure(LoginTicketErrors.WrongDevice);
        }

        if (ConsumedAt is not null)
        {
            return Result.Failure(LoginTicketErrors.AlreadyUsed);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(LoginTicketErrors.Expired);
        }

        if (NoticeVersion is not null && acceptedNoticeVersion != NoticeVersion)
        {
            return Result.Failure(LoginTicketErrors.NoticeVersionMismatch);
        }

        ConsumedAt = now;
        return Result.Success();
    }
}

public static class LoginTicketErrors
{
    public static readonly Error WrongDevice = new("LoginTicket.WrongDevice", "El inicio de sesión no es válido en este puesto.");

    public static readonly Error AlreadyUsed = new("LoginTicket.AlreadyUsed", "El inicio de sesión ya se completó o se canceló.");

    public static readonly Error Expired = new("LoginTicket.Expired", "El inicio de sesión venció. Volvé a ingresar.");

    public static readonly Error NoticeVersionMismatch = new(
        "LoginTicket.NoticeVersionMismatch",
        "Hay que aceptar la versión vigente del aviso de privacidad.");
}
