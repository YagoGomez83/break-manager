using BreakManager.Application.Common;
using BreakManager.Application.Identity;
using BreakManager.Domain.Common;
using BreakManager.Domain.Presence;
using Microsoft.Extensions.Options;

namespace BreakManager.Application.Presence;

internal sealed class WorkstationSessionService(
    IWorkstationSessionRepository sessions,
    ITokenService tokens,
    TimeProvider time,
    IOptions<IdentityParameters> parameters) : IWorkstationSessionService
{
    public async Task<OpenedSession> OpenAsync(Guid workstationId, Guid operatorId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var onWorkstation = await sessions.GetOpenForWorkstationAsync(workstationId, cancellationToken).ConfigureAwait(false);
        var forOperator = await sessions.GetOpenForOperatorAsync(operatorId, cancellationToken).ConfigureAwait(false);

        // Si el operador ya está en este puesto, es la misma sesión: no se pasa dos veces.
        if (forOperator is not null && forOperator.WorkstationId == workstationId)
        {
            forOperator = null;
        }

        var token = tokens.GenerateToken();
        var opening = WorkstationSession.Open(
            onWorkstation,
            forOperator,
            Guid.CreateVersion7(now),
            workstationId,
            operatorId,
            tokens.Hash(token),
            now,
            parameters.Value.AgentSessionLifetime);

        if (opening.IsNew)
        {
            sessions.Add(opening.Session);
        }

        var closed = opening.Closed
            .Select(s => new ClosedSession(s.Id, s.WorkstationId, s.OperatorId, s.EndReason!.Value))
            .ToList();

        return new OpenedSession(opening.Session.Id, token, opening.Session.ExpiresAt, closed);
    }

    public async Task<SessionInfo?> FindUsableByTokenAsync(string sessionToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var session = await sessions.GetByTokenHashAsync(tokens.Hash(sessionToken), cancellationToken).ConfigureAwait(false);
        return session is not null && session.IsUsable(time.GetUtcNow())
            ? new SessionInfo(session.Id, session.WorkstationId, session.OperatorId)
            : null;
    }

    public async Task<Result> CloseAsync(Guid sessionId, SessionEndReason reason, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return session is null
            ? Result.Failure(WorkstationSessionErrors.NotFound)
            : session.End(time.GetUtcNow(), reason);
    }

    public async Task<Guid?> CloseOpenForWorkstationAsync(Guid workstationId, SessionEndReason reason, CancellationToken cancellationToken)
    {
        var session = await sessions.GetOpenForWorkstationAsync(workstationId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        session.End(time.GetUtcNow(), reason);
        return session.Id;
    }
}
