using BreakManager.Application.Common;
using BreakManager.Application.Identity;
using BreakManager.Application.Presence;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BreakManager.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(BreakManagerDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Los datos cambiaron mientras se guardaban.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConcurrencyConflictException("Otro request guardó un dato que debía ser único.", ex);
        }
    }
}

internal sealed class EfOperatorRepository(BreakManagerDbContext dbContext) : IOperatorRepository
{
    public Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Operator>().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Operator?> GetByEmployeeNumberAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken) =>
        dbContext.Set<Operator>().SingleOrDefaultAsync(o => o.EmployeeNumber == employeeNumber, cancellationToken);

    public Task<bool> ExistsAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken) =>
        dbContext.Set<Operator>().AnyAsync(o => o.EmployeeNumber == employeeNumber, cancellationToken);

    public void Add(Operator op) => dbContext.Add(op);
}

internal sealed class EfWorkstationRepository(BreakManagerDbContext dbContext) : IWorkstationRepository
{
    public Task<Workstation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Workstation>().SingleOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken) =>
        dbContext.Set<Workstation>().AnyAsync(w => w.Number == number, cancellationToken);

    public Task<Workstation?> GetByCredentialTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Set<Workstation>().SingleOrDefaultAsync(w => w.Credentials.Any(c => c.TokenHash == tokenHash), cancellationToken);

    public void Add(Workstation workstation) => dbContext.Add(workstation);
}

internal sealed class EfEnrollmentCodeRepository(BreakManagerDbContext dbContext) : IEnrollmentCodeRepository
{
    public Task<EnrollmentCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken) =>
        dbContext.Set<EnrollmentCode>().SingleOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);

    public async Task<IReadOnlyList<EnrollmentCode>> ListPendingForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken) =>
        await dbContext.Set<EnrollmentCode>()
            .Where(c => c.WorkstationId == workstationId && c.RedeemedAt == null && c.InvalidatedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(EnrollmentCode code) => dbContext.Add(code);
}

internal sealed class EfLoginTicketRepository(BreakManagerDbContext dbContext) : ILoginTicketRepository
{
    public Task<LoginTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Set<LoginTicket>().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(LoginTicket ticket) => dbContext.Add(ticket);
}

internal sealed class EfPrivacyNoticeRepository(BreakManagerDbContext dbContext) : IPrivacyNoticeRepository
{
    public Task<PrivacyNotice?> GetCurrentAsync(CancellationToken cancellationToken) =>
        dbContext.Set<PrivacyNotice>().OrderByDescending(n => n.Version).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasAcceptedAsync(Guid operatorId, int version, CancellationToken cancellationToken) =>
        dbContext.Set<NoticeAcceptance>().AnyAsync(a => a.OperatorId == operatorId && a.NoticeVersion == version, cancellationToken);

    public void Add(PrivacyNotice notice) => dbContext.Add(notice);

    public void AddAcceptance(NoticeAcceptance acceptance) => dbContext.Add(acceptance);
}

internal sealed class EfWorkstationSessionRepository(BreakManagerDbContext dbContext) : IWorkstationSessionRepository
{
    public Task<WorkstationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<WorkstationSession>().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<WorkstationSession?> GetOpenForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken) =>
        dbContext.Set<WorkstationSession>().SingleOrDefaultAsync(s => s.WorkstationId == workstationId && s.EndedAt == null, cancellationToken);

    public Task<WorkstationSession?> GetOpenForOperatorAsync(Guid operatorId, CancellationToken cancellationToken) =>
        dbContext.Set<WorkstationSession>().SingleOrDefaultAsync(s => s.OperatorId == operatorId && s.EndedAt == null, cancellationToken);

    public Task<WorkstationSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Set<WorkstationSession>().SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public void Add(WorkstationSession session) => dbContext.Add(session);
}
