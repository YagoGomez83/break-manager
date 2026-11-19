using BreakManager.Application.Abstractions;
using BreakManager.Application.Identity.Queries;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using BreakManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BreakManager.Infrastructure.Queries;

// Proyecciones de lectura para las páginas de Admin (sin tracking).

internal sealed class ListWorkstationsHandler(BreakManagerDbContext dbContext)
    : IQueryHandler<ListWorkstationsQuery, IReadOnlyList<WorkstationListItem>>
{
    public async Task<IReadOnlyList<WorkstationListItem>> HandleAsync(ListWorkstationsQuery query, CancellationToken cancellationToken)
    {
        var workstations = await dbContext.Set<Workstation>()
            .AsNoTracking()
            .OrderBy(w => w.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. workstations.Select(w => new WorkstationListItem(
                w.Id,
                w.Number,
                w.Row,
                w.Column,
                w.Hostname,
                w.ServiceStatus,
                w.Revoked,
                Enrolled: w.ActiveCredential is not null,
                w.ActiveCredential?.LastSeenAt,
                w.ActiveCredential?.LastSeenIp)),
        ];
    }
}

internal sealed class ListOperatorsHandler(BreakManagerDbContext dbContext)
    : IQueryHandler<ListOperatorsQuery, IReadOnlyList<OperatorListItem>>
{
    public async Task<IReadOnlyList<OperatorListItem>> HandleAsync(ListOperatorsQuery query, CancellationToken cancellationToken)
    {
        var operators = await dbContext.Set<Operator>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. operators
                .OrderBy(o => o.EmployeeNumber.Value, StringComparer.Ordinal)
                .Select(o => new OperatorListItem(
                    o.Id,
                    o.EmployeeNumber.Value,
                    o.FullName,
                    o.Group,
                    o.HomeWorkstationNumber,
                    o.Active,
                    o.MustChangePin,
                    o.LockedUntil,
                    o.RequiresManualUnlock)),
        ];
    }
}

internal sealed class ListPrivacyNoticesHandler(BreakManagerDbContext dbContext)
    : IQueryHandler<ListPrivacyNoticesQuery, IReadOnlyList<PrivacyNoticeListItem>>
{
    public async Task<IReadOnlyList<PrivacyNoticeListItem>> HandleAsync(ListPrivacyNoticesQuery query, CancellationToken cancellationToken) =>
        await dbContext.Set<PrivacyNotice>()
            .AsNoTracking()
            .OrderByDescending(n => n.Version)
            .Select(n => new PrivacyNoticeListItem(n.Id, n.Version, n.Text, n.PublishedAt, n.PublishedByUserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

internal sealed class ListOpenSessionsHandler(BreakManagerDbContext dbContext)
    : IQueryHandler<ListOpenSessionsQuery, IReadOnlyList<OpenSessionListItem>>
{
    public async Task<IReadOnlyList<OpenSessionListItem>> HandleAsync(ListOpenSessionsQuery query, CancellationToken cancellationToken)
    {
        var rows = await (
                from session in dbContext.Set<WorkstationSession>().AsNoTracking()
                where session.EndedAt == null
                join workstation in dbContext.Set<Workstation>().AsNoTracking() on session.WorkstationId equals workstation.Id
                join op in dbContext.Set<Operator>().AsNoTracking() on session.OperatorId equals op.Id
                orderby workstation.Number
                select new { session.Id, workstation.Number, workstation.Hostname, op.EmployeeNumber, session.StartedAt, session.ExpiresAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(r => new OpenSessionListItem(r.Id, r.Number, r.Hostname, r.EmployeeNumber.Value, r.StartedAt, r.ExpiresAt)),
        ];
    }
}
