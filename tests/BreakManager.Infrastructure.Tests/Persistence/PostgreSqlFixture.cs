using Testcontainers.PostgreSql;

namespace BreakManager.Infrastructure.Tests.Persistence;

/// <summary>
/// Levanta un PostgreSQL real en un contenedor (Testcontainers) compartido por los tests de la clase.
/// La versión mayor coincide con la de deploy/docker-compose.dev.yml.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
