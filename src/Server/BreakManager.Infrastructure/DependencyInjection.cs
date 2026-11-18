using BreakManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "BreakManager";

    /// <summary>
    /// Registra la persistencia (EF Core + PostgreSQL con snake_case) y el reloj del sistema.
    /// La cadena de conexión se lee de <c>ConnectionStrings:BreakManager</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'.");

        services.AddDbContext<BreakManagerDbContext>(options => Configure(options, connectionString));
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Agrega el health check de la base de datos, con la etiqueta <c>ready</c>.
    /// </summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(this IHealthChecksBuilder builder, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDbContextCheck<BreakManagerDbContext>("database", tags: tags);
    }

    /// <summary>
    /// Aplica las migraciones pendientes. Se invoca solo con un comando explícito (nunca al arrancar).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BreakManagerDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
