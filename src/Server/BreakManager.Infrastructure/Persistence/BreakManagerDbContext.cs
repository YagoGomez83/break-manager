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
