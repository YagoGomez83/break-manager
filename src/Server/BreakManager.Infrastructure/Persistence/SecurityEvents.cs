using System.Text.Json;
using BreakManager.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BreakManager.Infrastructure.Persistence;

/// <summary>
/// Fila de la tabla append-only <c>security_events</c>. Un trigger de la base rechaza UPDATE, DELETE y TRUNCATE.
/// </summary>
internal sealed class SecurityEventRecord
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public required string ActorType { get; init; }

    public Guid? ActorId { get; init; }

    public Guid? WorkstationId { get; init; }

    /// <summary>JSON (jsonb) con IDs y datos técnicos; nunca nombres ni secretos.</summary>
    public string? Data { get; init; }

    public static SecurityEventRecord From(SecurityEvent securityEvent) => new()
    {
        Id = securityEvent.Id,
        Type = securityEvent.Type.ToString(),
        OccurredAt = securityEvent.OccurredAt,
        ActorType = securityEvent.ActorType.ToString(),
        ActorId = securityEvent.ActorId,
        WorkstationId = securityEvent.WorkstationId,
        Data = securityEvent.Data is null ? null : JsonSerializer.Serialize(securityEvent.Data),
    };
}

internal sealed class SecurityEventRecordConfiguration : IEntityTypeConfiguration<SecurityEventRecord>
{
    public void Configure(EntityTypeBuilder<SecurityEventRecord> builder)
    {
        builder.ToTable("security_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Type).HasMaxLength(60);
        builder.Property(e => e.ActorType).HasMaxLength(20);
        builder.Property(e => e.Data).HasColumnType("jsonb");
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.Type, e.OccurredAt });
    }
}

internal sealed class EfSecurityEventLog(BreakManagerDbContext dbContext) : ISecurityEventLog
{
    public void Record(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        dbContext.Set<SecurityEventRecord>().Add(SecurityEventRecord.From(securityEvent));
    }
}
