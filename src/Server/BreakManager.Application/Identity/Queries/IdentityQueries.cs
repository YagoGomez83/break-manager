using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity.Queries;

// Consultas de las páginas de Admin. Son proyecciones de lectura (CQRS liviano, 03-arquitectura.md §3):
// los handlers (IQueryHandler) los implementa Infrastructure leyendo directo de la base.

public sealed record ListWorkstationsQuery;

public sealed record WorkstationListItem(
    Guid Id,
    int Number,
    int Row,
    int Column,
    string Hostname,
    ServiceStatus ServiceStatus,
    bool Revoked,
    bool Enrolled,
    DateTimeOffset? LastSeenAt,
    string? LastSeenIp);

public sealed record ListOperatorsQuery;

public sealed record OperatorListItem(
    Guid Id,
    string EmployeeNumber,
    string FullName,
    Group Group,
    int? HomeWorkstationNumber,
    bool Active,
    bool MustChangePin,
    DateTimeOffset? LockedUntil,
    bool RequiresManualUnlock);

public sealed record ListPrivacyNoticesQuery;

public sealed record PrivacyNoticeListItem(Guid Id, int Version, string Text, DateTimeOffset PublishedAt, Guid PublishedByUserId);

/// <summary>Sesiones abiertas: lo que muestra "aparece en el servidor" (criterio de salida de la Fase 2).</summary>
public sealed record ListOpenSessionsQuery;

public sealed record OpenSessionListItem(
    Guid SessionId,
    int WorkstationNumber,
    string Hostname,
    string EmployeeNumber,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt);
