using BreakManager.Domain.Common;
using BreakManager.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BreakManager.Infrastructure.Persistence;

/// <summary>
/// Contexto único del monolito (ADR-0001), con las tablas de Identity y las claves de DataProtection (ADR-0011).
/// Las configuraciones de entidades se aplican por escaneo (<see cref="IEntityTypeConfiguration{TEntity}"/>).
/// </summary>
internal sealed class BreakManagerDbContext(DbContextOptions<BreakManagerDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(BreakManagerDbContext).Assembly);

        // IdentityDbContext fija nombres AspNet*; se pasan a snake_case como el resto del esquema.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // Los eventos de dominio no se persisten.
        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => IsAggregateRoot(t.ClrType)))
        {
            builder.Entity(entityType.ClrType).Ignore(nameof(AggregateRoot<Guid>.DomainEvents));
        }
    }

    private static bool IsAggregateRoot(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
        }

        return false;
    }
}
