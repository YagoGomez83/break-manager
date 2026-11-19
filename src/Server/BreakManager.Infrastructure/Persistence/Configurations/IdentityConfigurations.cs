using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BreakManager.Infrastructure.Persistence.Configurations;

internal sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable("operators");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.EmployeeNumber)
            .HasConversion(v => v.Value, v => EmployeeNumber.Create(v).Value)
            .HasMaxLength(EmployeeNumber.MaxLength);
        builder.HasIndex(o => o.EmployeeNumber).IsUnique();

        builder.Property(o => o.FullName).HasMaxLength(Operator.MaxFullNameLength);
        builder.Property(o => o.Group).HasConversion<string>().HasMaxLength(1);
        builder.Property(o => o.PinHash).HasMaxLength(256);

        builder.PrimitiveCollection<List<DateTimeOffset>>("_recentLockouts").HasColumnName("recent_lockouts");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

internal sealed class WorkstationConfiguration : IEntityTypeConfiguration<Workstation>
{
    public void Configure(EntityTypeBuilder<Workstation> builder)
    {
        builder.ToTable("workstations");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Number);
        builder.HasIndex(w => w.Number).IsUnique();
        builder.Property(w => w.Hostname).HasMaxLength(Workstation.MaxHostnameLength);
        builder.Property(w => w.ServiceStatus).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(w => w.ActiveCredential);

        builder.HasMany(w => w.Credentials)
            .WithOne()
            .HasForeignKey("WorkstationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(w => w.Credentials).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

internal sealed class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(EntityTypeBuilder<DeviceCredential> builder)
    {
        builder.ToTable("device_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TokenHash).HasMaxLength(64);
        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.Property(c => c.IssuedAt);
        builder.Property(c => c.LastSeenIp).HasMaxLength(45);
    }
}

internal sealed class EnrollmentCodeConfiguration : IEntityTypeConfiguration<EnrollmentCode>
{
    public void Configure(EntityTypeBuilder<EnrollmentCode> builder)
    {
        builder.ToTable("enrollment_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CodeHash).HasMaxLength(64);
        builder.Property(c => c.IssuedAt);
        builder.Property(c => c.ExpiresAt);
        builder.HasIndex(c => c.CodeHash).IsUnique();
        builder.HasIndex(c => c.WorkstationId);
        builder.HasOne<Workstation>().WithMany().HasForeignKey(c => c.WorkstationId).OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

internal sealed class LoginTicketConfiguration : IEntityTypeConfiguration<LoginTicket>
{
    public void Configure(EntityTypeBuilder<LoginTicket> builder)
    {
        builder.ToTable("login_tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(64);
        builder.Property(t => t.NoticeVersion);
        builder.Property(t => t.RequiresPinChange);
        builder.Property(t => t.IssuedAt);
        builder.Property(t => t.ExpiresAt);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne<Operator>().WithMany().HasForeignKey(t => t.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeviceCredential>().WithMany().HasForeignKey(t => t.DeviceCredentialId).OnDelete(DeleteBehavior.Restrict);

        // Dos intentos simultáneos de completar el login con el mismo ticket: solo uno se guarda.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

internal sealed class PrivacyNoticeConfiguration : IEntityTypeConfiguration<PrivacyNotice>
{
    public void Configure(EntityTypeBuilder<PrivacyNotice> builder)
    {
        builder.ToTable("privacy_notices");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Version);
        builder.HasIndex(n => n.Version).IsUnique();
        builder.Property(n => n.Text).HasMaxLength(PrivacyNotice.MaxTextLength);
        builder.Property(n => n.PublishedAt);
        builder.Property(n => n.PublishedByUserId);
    }
}

internal sealed class NoticeAcceptanceConfiguration : IEntityTypeConfiguration<NoticeAcceptance>
{
    public void Configure(EntityTypeBuilder<NoticeAcceptance> builder)
    {
        builder.ToTable("notice_acceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.AcceptedAt);
        builder.HasIndex(a => new { a.OperatorId, a.NoticeVersion });
        builder.HasOne<Operator>().WithMany().HasForeignKey(a => a.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Workstation>().WithMany().HasForeignKey(a => a.WorkstationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PrivacyNotice>().WithMany().HasForeignKey(a => a.NoticeVersion)
            .HasPrincipalKey(n => n.Version).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder) =>
        builder.Property(u => u.Group).HasConversion<string>().HasMaxLength(1);
}
