using BreakManager.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BreakManager.Infrastructure.Tests.Persistence;

/// <summary>
/// Base de datos nueva (en el PostgreSQL del contenedor compartido) con las migraciones aplicadas,
/// y el contenedor de DI real de Application + Infrastructure. Una por test: los tests no comparten datos.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(ServiceProvider services, string connectionString)
    {
        Services = services;
        ConnectionString = connectionString;
    }

    public ServiceProvider Services { get; }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(PostgreSqlFixture server, CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(server.ConnectionString)
        {
            Database = "t_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await services.MigrateDatabaseAsync(cancellationToken);
        return new TestDatabase(services, connectionString);
    }

    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();

    public async Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T?> ScalarAsync<T>(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}
