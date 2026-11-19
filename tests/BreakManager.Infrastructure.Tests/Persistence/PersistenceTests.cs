using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Presence;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BreakManager.Infrastructure.Tests.Persistence;

public sealed class PersistenceTests(PostgreSqlFixture server) : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Operator_RoundTrips_WithItsLockoutState()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var hasher = db.Services.GetRequiredService<IPinHasher>();
        var number = EmployeeNumber.Create("20481").Value;
        var op = Operator.Create(Guid.CreateVersion7(), number, "Operador", Group.C, 3, hasher.Hash(Pin.Create("482913", number).Value)).Value;
        var policy = new LockoutPolicy(1, TimeSpan.FromMinutes(5), 3, TimeSpan.FromHours(24));
        op.AttemptLogin("000001", T0, policy, hasher);
        op.AttemptLogin("000001", T0.AddMinutes(10), policy, hasher);

        await using (var scope = db.NewScope())
        {
            scope.ServiceProvider.GetRequiredService<IOperatorRepository>().Add(op);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using (var scope = db.NewScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IOperatorRepository>().GetByEmployeeNumberAsync(number, Ct);

            Assert.NotNull(loaded);
            Assert.Equal(op.Id, loaded.Id);
            Assert.Equal(Group.C, loaded.Group);
            Assert.True(loaded.MustChangePin);
            Assert.Equal([T0, T0.AddMinutes(10)], loaded.RecentLockouts);
            Assert.Equal(T0.AddMinutes(15), loaded.LockedUntil);
            Assert.True(hasher.Verify(loaded.PinHash, "482913"));
        }
    }

    [Fact]
    public async Task Workstation_RoundTrips_WithCredentials_AndIsFoundByTokenHash()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var workstation = Workstation.Create(Guid.CreateVersion7(), 7, 2, 3, "PC-07").Value;
        workstation.Enroll(Guid.CreateVersion7(), "old-hash", T0);
        var credential = workstation.Enroll(Guid.CreateVersion7(), "new-hash", T0.AddHours(1));
        credential.RecordSeen(T0.AddHours(2), "10.0.0.7");

        await using (var scope = db.NewScope())
        {
            scope.ServiceProvider.GetRequiredService<IWorkstationRepository>().Add(workstation);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using (var scope = db.NewScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IWorkstationRepository>().GetByCredentialTokenHashAsync("new-hash", Ct);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Credentials.Count);
            Assert.Equal(credential.Id, loaded.ActiveCredential!.Id);
            Assert.Equal("10.0.0.7", loaded.ActiveCredential.LastSeenIp);
            Assert.Equal(T0.AddHours(2), loaded.ActiveCredential.LastSeenAt);
        }
    }

    [Fact]
    public async Task TwoOpenSessionsOnTheSameWorkstation_AreRejectedByTheDatabase()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var (workstationId, operatorA, operatorB) = await SeedWorkstationAndOperatorsAsync(db);

        await using var scope = db.NewScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IWorkstationSessionRepository>();
        sessions.Add(WorkstationSession.Open(null, null, Guid.CreateVersion7(), workstationId, operatorA, "a", T0, TimeSpan.FromHours(8)).Session);
        sessions.Add(WorkstationSession.Open(null, null, Guid.CreateVersion7(), workstationId, operatorB, "b", T0, TimeSpan.FromHours(8)).Session);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task ConsumingTheSameTicketConcurrently_OnlyOneSaveWins()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var (workstationId, operatorId, _) = await SeedWorkstationAndOperatorsAsync(db);
        Guid credentialId;
        await using (var scope = db.NewScope())
        {
            var workstation = await scope.ServiceProvider.GetRequiredService<IWorkstationRepository>().GetByIdAsync(workstationId, Ct);
            credentialId = workstation!.ActiveCredential!.Id;
            var ticket = LoginTicket.Issue(Guid.CreateVersion7(), operatorId, credentialId, "ticket-hash", null, false, T0, TimeSpan.FromMinutes(5));
            scope.ServiceProvider.GetRequiredService<ILoginTicketRepository>().Add(ticket);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using var first = db.NewScope();
        await using var second = db.NewScope();
        var ticketA = await first.ServiceProvider.GetRequiredService<ILoginTicketRepository>().GetByHashAsync("ticket-hash", Ct);
        var ticketB = await second.ServiceProvider.GetRequiredService<ILoginTicketRepository>().GetByHashAsync("ticket-hash", Ct);
        Assert.True(ticketA!.Consume(T0.AddMinutes(1), credentialId, null).IsSuccess);
        Assert.True(ticketB!.Consume(T0.AddMinutes(1), credentialId, null).IsSuccess);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task SecurityEvents_AreAppendOnly()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var id = Guid.CreateVersion7();
        await using (var scope = db.NewScope())
        {
            scope.ServiceProvider.GetRequiredService<ISecurityEventLog>().Record(new SecurityEvent(
                id,
                SecurityEventType.OperatorLoginFailed,
                T0,
                SecurityActorType.Operator,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Dictionary<string, string> { ["ip"] = "10.0.0.7" }));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        Assert.Equal("10.0.0.7", await db.ScalarAsync<string>($"SELECT data->>'ip' FROM security_events WHERE id = '{id}'", Ct));

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => db.ExecuteAsync("UPDATE security_events SET type = 'X'", Ct));
        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => db.ExecuteAsync("DELETE FROM security_events", Ct));
        var truncate = await Assert.ThrowsAsync<PostgresException>(
            () => db.ExecuteAsync("TRUNCATE security_events", Ct));

        Assert.All([update, delete, truncate], ex => Assert.Contains("append-only", ex.MessageText, StringComparison.Ordinal));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM security_events", Ct));
    }

    private static async Task<(Guid WorkstationId, Guid OperatorA, Guid OperatorB)> SeedWorkstationAndOperatorsAsync(TestDatabase db)
    {
        await using var scope = db.NewScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
        var workstation = Workstation.Create(Guid.CreateVersion7(), 7, 2, 3, "PC-07").Value;
        workstation.Enroll(Guid.CreateVersion7(), "device-hash", T0);
        scope.ServiceProvider.GetRequiredService<IWorkstationRepository>().Add(workstation);

        Operator NewOperator(string employee)
        {
            var number = EmployeeNumber.Create(employee).Value;
            return Operator.Create(Guid.CreateVersion7(), number, "Operador", Group.C, null, hasher.Hash(Pin.Create("482913", number).Value)).Value;
        }

        var a = NewOperator("1001");
        var b = NewOperator("1002");
        var operators = scope.ServiceProvider.GetRequiredService<IOperatorRepository>();
        operators.Add(a);
        operators.Add(b);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return (workstation.Id, a.Id, b.Id);
    }
}
