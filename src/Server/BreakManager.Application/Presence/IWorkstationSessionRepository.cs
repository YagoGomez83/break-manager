using BreakManager.Domain.Presence;

namespace BreakManager.Application.Presence;

public interface IWorkstationSessionRepository
{
    Task<WorkstationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkstationSession?> GetOpenForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken);

    Task<WorkstationSession?> GetOpenForOperatorAsync(Guid operatorId, CancellationToken cancellationToken);

    Task<WorkstationSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(WorkstationSession session);
}
