using BreakManager.Domain.Common;
using BreakManager.Domain.Presence;

namespace BreakManager.Application.Presence;

/// <summary>
/// Puerto de Presence para los otros módulos: abrir, buscar y cerrar sesiones de puesto
/// sin acceder a la entidad <see cref="WorkstationSession"/>. Los cambios se confirman con IUnitOfWork.
/// </summary>
public interface IWorkstationSessionService
{
    /// <summary>
    /// Abre la sesión (RN-16, RN-17) y devuelve el token en claro, una sola vez.
    /// </summary>
    Task<OpenedSession> OpenAsync(Guid workstationId, Guid operatorId, CancellationToken cancellationToken);

    /// <summary>Sesión utilizable (abierta y sin vencer) para ese token, o <c>null</c>.</summary>
    Task<SessionInfo?> FindUsableByTokenAsync(string sessionToken, CancellationToken cancellationToken);

    Task<Result> CloseAsync(Guid sessionId, SessionEndReason reason, CancellationToken cancellationToken);

    /// <summary>Cierra la sesión abierta del puesto, si hay. Devuelve el ID de la sesión cerrada.</summary>
    Task<Guid?> CloseOpenForWorkstationAsync(Guid workstationId, SessionEndReason reason, CancellationToken cancellationToken);
}

public sealed record OpenedSession(Guid SessionId, string Token, DateTimeOffset ExpiresAt, IReadOnlyList<ClosedSession> Closed);

public sealed record ClosedSession(Guid SessionId, Guid WorkstationId, Guid OperatorId, SessionEndReason Reason);

public sealed record SessionInfo(Guid SessionId, Guid WorkstationId, Guid OperatorId);
