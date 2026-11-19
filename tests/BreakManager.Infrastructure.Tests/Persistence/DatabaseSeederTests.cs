using BreakManager.Infrastructure.Persistence;

namespace BreakManager.Infrastructure.Tests.Persistence;

public sealed class DatabaseSeederTests(PostgreSqlFixture server) : IClassFixture<PostgreSqlFixture>
{
    private const string AdminPassword = "mate-cocido-con-tostadas-7";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_CreatesRolesAdminAndDraftNotice_InDevelopment()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);

        var report = await db.Services.SeedDatabaseAsync(new SeedSettings("admin", AdminPassword, IsDevelopment: true), Ct);

        Assert.True(report.IsSuccess);
        Assert.True(report.Value.AdminCreated);
        Assert.True(report.Value.DraftNoticePublished);
        Assert.Equal(4L, await db.ScalarAsync<long>("SELECT count(*) FROM roles", Ct));
        Assert.True(await db.ScalarAsync<bool>("SELECT must_change_password FROM users WHERE user_name = 'admin'", Ct));
        Assert.StartsWith("BORRADOR", await db.ScalarAsync<string>("SELECT text FROM privacy_notices", Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_OutsideDevelopment_DoesNotPublishTheDraftNotice()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);

        var report = await db.Services.SeedDatabaseAsync(new SeedSettings("admin", AdminPassword, IsDevelopment: false), Ct);

        Assert.False(report.Value.DraftNoticePublished);
        Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM privacy_notices", Ct));
    }

    [Fact]
    public async Task Seed_IsIdempotent_AndDoesNotCreateASecondAdmin()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await db.Services.SeedDatabaseAsync(new SeedSettings("admin", AdminPassword, IsDevelopment: true), Ct);

        var again = await db.Services.SeedDatabaseAsync(new SeedSettings("otro-admin", AdminPassword, IsDevelopment: true), Ct);

        Assert.False(again.Value.AdminCreated);
        Assert.False(again.Value.DraftNoticePublished);
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM users", Ct));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM privacy_notices", Ct));
    }

    [Fact]
    public async Task Seed_WithoutBootstrapVariables_CreatesNoAdmin()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);

        var report = await db.Services.SeedDatabaseAsync(new SeedSettings(null, null, IsDevelopment: false), Ct);

        Assert.True(report.IsSuccess);
        Assert.False(report.Value.AdminCreated);
    }

    [Fact]
    public async Task Seed_WithACommonBootstrapPassword_Fails()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);

        var report = await db.Services.SeedDatabaseAsync(new SeedSettings("admin", "qwerty123456", IsDevelopment: false), Ct);

        Assert.True(report.IsFailure);
        Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM users", Ct));
    }

    [Fact]
    public void SeedSettings_ToString_DoesNotRevealThePassword()
    {
        Assert.DoesNotContain(AdminPassword, new SeedSettings("admin", AdminPassword, true).ToString(), StringComparison.Ordinal);
    }
}
