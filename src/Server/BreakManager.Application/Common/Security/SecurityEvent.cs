namespace BreakManager.Application.Common.Security;

/// <summary>
/// Evento de seguridad (tabla append-only <c>security_events</c>). <see cref="Data"/> no lleva nombres ni secretos:
/// solo IDs, IPs y datos técnicos.
/// </summary>
public sealed record SecurityEvent(
    Guid Id,
    SecurityEventType Type,
    DateTimeOffset OccurredAt,
    SecurityActorType ActorType,
    Guid? ActorId,
    Guid? WorkstationId,
    IReadOnlyDictionary<string, string>? Data = null);

public enum SecurityEventType
{
    OperatorLoginFailed,
    OperatorLockedOut,
    OperatorUnlocked,
    OperatorPinReset,
    OperatorPinChanged,
    LoginTicketRejected,
    UserLoginFailed,
    UserLockedOut,
    EnrollmentCodeGenerated,
    WorkstationEnrolled,
    WorkstationRevoked,
    InvalidDeviceToken,
    DeviceTokenIpChanged,
    PrivacyNoticePublished,
}

public enum SecurityActorType
{
    Operator,
    User,
    Device,
    System,
}

/// <summary>
/// Registra eventos de seguridad dentro de la unidad de trabajo del request (se guardan con <see cref="IUnitOfWork"/>).
/// </summary>
public interface ISecurityEventLog
{
    void Record(SecurityEvent securityEvent);
}
