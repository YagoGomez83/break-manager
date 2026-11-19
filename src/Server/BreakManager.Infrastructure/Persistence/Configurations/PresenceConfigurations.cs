using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BreakManager.Infrastructure.Persistence.Configurations;

internal sealed class WorkstationSessionConfiguration : IEntityTypeConfiguration<WorkstationSession>
{
    public void Configure(EntityTypeBuilder<WorkstationSession> builder)
    {
        builder.ToTable("workstation_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).HasMaxLength(64);
        builder.Property(s => s.StartedAt);
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.Property(s => s.EndReason).HasConversion<string>().HasMaxLength(40);

        builder.HasOne<Workstation>().WithMany().HasForeignKey(s => s.WorkstationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Operator>().WithMany().HasForeignKey(s => s.OperatorId).OnDelete(DeleteBehavior.Restrict);

        // RN-16 también en la base: una sesión abierta por puesto y una por operador.
        builder.HasIndex(s => s.WorkstationId).IsUnique().HasFilter("ended_at IS NULL")
            .HasDatabaseName("ux_workstation_sessions_open_per_workstation");
        builder.HasIndex(s => s.OperatorId).IsUnique().HasFilter("ended_at IS NULL")
            .HasDatabaseName("ux_workstation_sessions_open_per_operator");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
