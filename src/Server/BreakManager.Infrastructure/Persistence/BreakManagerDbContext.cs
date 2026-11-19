using Microsoft.EntityFrameworkCore;

namespace BreakManager.Infrastructure.Persistence;

/// <summary>
/// Contexto único del monolito (ADR-0001). Las configuraciones de entidades se agregan por módulo
/// en las fases siguientes mediante <see cref="IEntityTypeConfiguration{TEntity}"/>.
/// </summary>
internal sealed class BreakManagerDbContext(DbContextOptions<BreakManagerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BreakManagerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
