using System.Text.Json.Serialization;

namespace BreakManager.Contracts.Agent;

/// <summary>Rutas de la API del agente (03-arquitectura.md §5, ADR-0010).</summary>
public static class AgentApiRoutes
{
    public const string Enrollment = "/api/agent/enrollment";
    public const string Sessions = "/api/agent/sessions";
    public const string SessionCompletion = "/api/agent/sessions/completion";
    public const string CurrentSession = "/api/agent/sessions/current";
}

/// <summary>Cabeceras de autenticación del agente (ADR-0010).</summary>
public static class AgentHeaders
{
    /// <summary>Esquema de <c>Authorization</c> con el token de dispositivo: <c>Authorization: Device &lt;token&gt;</c>.</summary>
    public const string DeviceScheme = "Device";

    /// <summary>Token de la sesión del operador, en las llamadas que dependen de una sesión.</summary>
    public const string SessionToken = "X-Session-Token";
}

/// <summary>Canje del código de enrolamiento (sin token de dispositivo).</summary>
public sealed record EnrollRequest(string Code)
{
    public override string ToString() => "EnrollRequest { *** }";
}

/// <summary>El token de dispositivo se entrega una sola vez; el agente lo guarda con DPAPI.</summary>
public sealed record EnrollResponse(Guid WorkstationId, int WorkstationNumber, string DeviceToken)
{
    public override string ToString() => $"EnrollResponse {{ WorkstationNumber = {WorkstationNumber} }}";
}

/// <summary>Paso 1 del login: legajo + PIN.</summary>
public sealed record StartLoginRequest(string EmployeeNumber, string Pin)
{
    public override string ToString() => "StartLoginRequest { *** }";
}

/// <summary>Paso 2 del login: ticket, versión del aviso aceptada y PIN nuevo, si se pidieron.</summary>
public sealed record CompleteLoginRequest(string Ticket, int? AcceptedNoticeVersion, string? NewPin)
{
    public override string ToString() => $"CompleteLoginRequest {{ AcceptedNoticeVersion = {AcceptedNoticeVersion} }}";
}

[JsonConverter(typeof(JsonStringEnumConverter<LoginStatus>))]
public enum LoginStatus
{
    Opened,
    StepsRequired,
}

/// <summary>
/// Respuesta de los dos pasos del login: <see cref="Session"/> si <see cref="Status"/> es <c>Opened</c>,
/// <see cref="Steps"/> si es <c>StepsRequired</c>.
/// </summary>
public sealed record LoginResponse(LoginStatus Status, SessionDto? Session, PendingStepsDto? Steps);

public sealed record SessionDto(Guid SessionId, string SessionToken, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"SessionDto {{ SessionId = {SessionId}, ExpiresAt = {ExpiresAt:O} }}";
}

/// <summary>Pasos pendientes antes de abrir la sesión, con el ticket de un solo uso.</summary>
public sealed record PendingStepsDto(string Ticket, DateTimeOffset ExpiresAt, PrivacyNoticeDto? Notice, bool RequiresPinChange)
{
    public override string ToString() =>
        $"PendingStepsDto {{ NoticeVersion = {Notice?.Version}, RequiresPinChange = {RequiresPinChange} }}";
}

public sealed record PrivacyNoticeDto(int Version, string Text);

/// <summary>Error de negocio: <see cref="Code"/> es estable; <see cref="Message"/> se muestra al operador.</summary>
public sealed record AgentError(string Code, string Message);
