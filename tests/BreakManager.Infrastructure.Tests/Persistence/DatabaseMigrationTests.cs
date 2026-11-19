using BreakManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Infrastructure.Tests.Persistence;

public sealed class DatabaseMigrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = database.ConnectionString,
            })
            .Build();

        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }

    [Fact]
    public async Task MigrateDatabaseAsync_AppliesAllMigrations_AndDatabaseIsReachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();

        await provider.MigrateDatabaseAsync(cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BreakManagerDbContext>();

        Assert.True(await dbContext.Database.CanConnectAsync(cancellationToken));
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync(cancellationToken));
        var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Contains(applied, migration => migration.EndsWith("_AddIdentityModule", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MigrationsHistoryTable_UsesSnakeCaseName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        await provider.MigrateDatabaseAsync(cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BreakManagerDbContext>();

        var exists = await dbContext.Database
            .SqlQuery<bool>($"SELECT to_regclass('public.__ef_migrations_history') IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);

        Assert.True(exists);
    }

    [Fact]
    public void AddInfrastructure_RegistersSystemTimeProvider()
    {
        using var provider = BuildProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddInfrastructure_WithoutConnectionString_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));
    }
}
