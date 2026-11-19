using BreakManager.Domain.Common;

namespace BreakManager.Domain.Presence;

/// <summary>
/// Período en que un operador está logueado en un puesto (RN-16, RN-17). El token de sesión se guarda como hash (ADR-0010).
/// Hay como máximo una sesión abierta por puesto y una por operador.
/// </summary>
public sealed class WorkstationSession : AggregateRoot<Guid>
{
    private WorkstationSession(
        Guid id,
        Guid workstationId,
        Guid operatorId,
        string tokenHash,
        DateTimeOffset startedAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        WorkstationId = workstationId;
        OperatorId = operatorId;
        TokenHash = tokenHash;
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
    }

    public Guid WorkstationId { get; }

    public Guid OperatorId { get; }

    public string TokenHash { get; private set; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>Vencimiento del token de sesión (<c>AgentSessionLifetime</c>).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public SessionEndReason? EndReason { get; private set; }

    public bool IsOpen => EndedAt is null;

    /// <summary>La sesión sirve para autenticar requests: abierta y con el token sin vencer.</summary>
    public bool IsUsable(DateTimeOffset now) => IsOpen && now < ExpiresAt;

    /// <summary>
    /// Abre la sesión de un operador en un puesto, cerrando las que lo impiden en la misma operación:
    /// la del puesto, si es de otro operador, con <see cref="SessionEndReason.Relief"/>; y la del operador
    /// en otro puesto con <see cref="SessionEndReason.MovedToAnotherWorkstation"/>.
    /// Si el mismo operador ya tiene la sesión abierta en ese puesto (p. ej. se reinició el agente),
    /// se renueva el token sin cerrarla.
    /// </summary>
    /// <param name="openOnWorkstation">Sesión abierta en el puesto, si hay.</param>
    /// <param name="openForOperator">Sesión abierta del operador, si hay.</param>
    public static SessionOpening Open(
        WorkstationSession? openOnWorkstation,
        WorkstationSession? openForOperator,
        Guid newSessionId,
        Guid workstationId,
        Guid operatorId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        EnsureMatches(openOnWorkstation, s => s.WorkstationId == workstationId, nameof(openOnWorkstation));
        EnsureMatches(openForOperator, s => s.OperatorId == operatorId, nameof(openForOperator));

        if (openOnWorkstation is not null && openOnWorkstation.OperatorId == operatorId)
        {
            openOnWorkstation.TokenHash = tokenHash;
            openOnWorkstation.ExpiresAt = now + lifetime;
            return new SessionOpening(openOnWorkstation, IsNew: false, []);
        }

        var closed = new List<WorkstationSession>(2);

        if (openOnWorkstation is not null)
        {
            openOnWorkstation.Close(now, SessionEndReason.Relief);
            closed.Add(openOnWorkstation);
        }

        if (openForOperator is not null)
        {
            openForOperator.Close(now, SessionEndReason.MovedToAnotherWorkstation);
            closed.Add(openForOperator);
        }

        var session = new WorkstationSession(newSessionId, workstationId, operatorId, tokenHash, now, now + lifetime);
        return new SessionOpening(session, IsNew: true, closed);
    }

    public Result End(DateTimeOffset now, SessionEndReason reason)
    {
        if (!IsOpen)
        {
            return Result.Failure(WorkstationSessionErrors.AlreadyEnded);
        }

        Close(now, reason);
        return Result.Success();
    }

    private void Close(DateTimeOffset now, SessionEndReason reason)
    {
        EndedAt = now;
        EndReason = reason;
    }

    private static void EnsureMatches(WorkstationSession? session, Func<WorkstationSession, bool> matches, string paramName)
    {
        if (session is not null && (!session.IsOpen || !matches(session)))
        {
            throw new ArgumentException("La sesión indicada no está abierta o no corresponde.", paramName);
        }
    }
}

/// <summary>Motivo de cierre de una sesión (02-dominio.md §3, R16).</summary>
public enum SessionEndReason
{
    Logout,

    /// <summary>Otro operador toma el puesto (RN-17).</summary>
    Relief,

    /// <summary>El mismo operador abre sesión en otro puesto (RN-16).</summary>
    MovedToAnotherWorkstation,

    /// <summary>El Admin revoca el puesto (ADR-0010).</summary>
    Revocation,

    Disconnection,

    EndOfShift,
}

/// <summary>Resultado de <see cref="WorkstationSession.Open"/>: la sesión vigente y las que se cerraron.</summary>
public sealed record SessionOpening(WorkstationSession Session, bool IsNew, IReadOnlyList<WorkstationSession> Closed);

public static class WorkstationSessionErrors
{
    public static readonly Error AlreadyEnded = new("WorkstationSession.AlreadyEnded", "La sesión ya estaba cerrada.");
}
