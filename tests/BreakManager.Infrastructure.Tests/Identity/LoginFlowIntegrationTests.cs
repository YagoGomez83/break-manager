using BreakManager.Application.Abstractions;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Administration;
using BreakManager.Application.Identity.Login;
using BreakManager.Application.Identity.Queries;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Infrastructure.Tests.Identity;

/// <summary>
/// Flujo completo con los handlers reales contra PostgreSQL: enrolar → login → aviso → cambio de PIN → sesión.
/// </summary>
public sealed class LoginFlowIntegrationTests(PostgreSqlFixture server) : IClassFixture<PostgreSqlFixture>
{
    private static readonly Guid Admin = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstLogin_EndsWithAnOpenSessionListedForTheAdmin()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var device = await EnrollAsync(db, 7);
        await PublishNoticeAsync(db, "Aviso de prueba");
        await CreateOperatorAsync(db, "20481", "482913");

        var start = await SendAsync<StartOperatorLoginCommand, Result<LoginOutcome>>(
            db, new StartOperatorLoginCommand(device.WorkstationId, device.CredentialId, "20481", "482913"));
        var pending = Assert.IsType<LoginOutcome.StepsRequired>(start.Value);
        Assert.True(pending.RequiresPinChange);
        Assert.Equal(1, pending.Notice!.Version);

        var complete = await SendAsync<CompleteOperatorLoginCommand, Result<LoginOutcome>>(
            db, new CompleteOperatorLoginCommand(device.WorkstationId, device.CredentialId, pending.Ticket, 1, "736251"));
        Assert.IsType<LoginOutcome.Opened>(complete.Value);

        var open = await QueryAsync<ListOpenSessionsQuery, IReadOnlyList<OpenSessionListItem>>(db, new ListOpenSessionsQuery());
        var row = Assert.Single(open);
        Assert.Equal(7, row.WorkstationNumber);
        Assert.Equal("20481", row.EmployeeNumber);

        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM notice_acceptances", Ct));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM login_tickets WHERE consumed_at IS NOT NULL", Ct));
    }

    [Fact]
    public async Task TicketFromAnotherDevice_IsRejected_AndNotConsumed()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var device = await EnrollAsync(db, 7);
        var other = await EnrollAsync(db, 8);
        await PublishNoticeAsync(db, "Aviso");
        await CreateOperatorAsync(db, "20481", "482913");
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await SendAsync<StartOperatorLoginCommand, Result<LoginOutcome>>(
            db, new StartOperatorLoginCommand(device.WorkstationId, device.CredentialId, "20481", "482913"))).Value);

        var result = await SendAsync<CompleteOperatorLoginCommand, Result<LoginOutcome>>(
            db, new CompleteOperatorLoginCommand(other.WorkstationId, other.CredentialId, pending.Ticket, 1, "736251"));

        Assert.Equal(IdentityErrors.InvalidLoginTicket, result.Error);
        Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM login_tickets WHERE consumed_at IS NOT NULL", Ct));
        Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM workstation_sessions", Ct));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM security_events WHERE type = 'LoginTicketRejected'", Ct));
    }

    [Fact]
    public async Task MovingToAnotherWorkstation_ClosesThePreviousSession()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var first = await EnrollAsync(db, 7);
        var second = await EnrollAsync(db, 8);
        await PublishNoticeAsync(db, "Aviso");
        await CreateOperatorAsync(db, "20481", "482913");
        await LoginFullyAsync(db, first, "20481", "482913", "736251");

        var again = await SendAsync<StartOperatorLoginCommand, Result<LoginOutcome>>(
            db, new StartOperatorLoginCommand(second.WorkstationId, second.CredentialId, "20481", "736251"));

        Assert.IsType<LoginOutcome.Opened>(again.Value);
        Assert.Equal(
            "MovedToAnotherWorkstation",
            await db.ScalarAsync<string>("SELECT end_reason FROM workstation_sessions WHERE ended_at IS NOT NULL", Ct));
        var open = await QueryAsync<ListOpenSessionsQuery, IReadOnlyList<OpenSessionListItem>>(db, new ListOpenSessionsQuery());
        Assert.Equal(8, Assert.Single(open).WorkstationNumber);
    }

    [Fact]
    public async Task AnotherOperatorOnTheSameWorkstation_ClosesThePreviousSessionAsRelief()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var device = await EnrollAsync(db, 7);
        await PublishNoticeAsync(db, "Aviso");
        await CreateOperatorAsync(db, "20481", "482913");
        await CreateOperatorAsync(db, "30000", "918273");
        await LoginFullyAsync(db, device, "20481", "482913", "736251");

        await LoginFullyAsync(db, device, "30000", "918273", "647382");

        Assert.Equal("Relief", await db.ScalarAsync<string>("SELECT end_reason FROM workstation_sessions WHERE ended_at IS NOT NULL", Ct));
    }

    [Fact]
    public async Task RevokingTheWorkstation_ClosesTheSession_AndTheTokenStopsWorking()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        var device = await EnrollAsync(db, 7);
        await PublishNoticeAsync(db, "Aviso");
        await CreateOperatorAsync(db, "20481", "482913");
        await LoginFullyAsync(db, device, "20481", "482913", "736251");

        await SendAsync<RevokeWorkstationCommand, Result>(db, new RevokeWorkstationCommand(device.WorkstationId, Admin));

        Assert.Equal("Revocation", await db.ScalarAsync<string>("SELECT end_reason FROM workstation_sessions", Ct));
        var auth = await SendAsync<AuthenticateDeviceCommand, Result<AuthenticatedDevice>>(
            db, new AuthenticateDeviceCommand(device.DeviceToken, null, "10.0.0.7"));
        Assert.Equal(IdentityErrors.InvalidDeviceToken, auth.Error);
    }

    private sealed record Device(Guid WorkstationId, Guid CredentialId, string DeviceToken);

    private static async Task<Device> EnrollAsync(TestDatabase db, int number)
    {
        var id = (await SendAsync<CreateWorkstationCommand, Result<Guid>>(db, new CreateWorkstationCommand(number, 1, number, $"PC-{number:00}"))).Value;
        var code = (await SendAsync<GenerateEnrollmentCodeCommand, Result<GeneratedEnrollmentCode>>(db, new GenerateEnrollmentCodeCommand(id, Admin))).Value;
        var enrolled = (await SendAsync<RedeemEnrollmentCodeCommand, Result<EnrolledDevice>>(db, new RedeemEnrollmentCodeCommand(code.Code, "10.0.0.7"))).Value;
        var auth = (await SendAsync<AuthenticateDeviceCommand, Result<AuthenticatedDevice>>(
            db, new AuthenticateDeviceCommand(enrolled.DeviceToken, null, "10.0.0.7"))).Value;
        return new Device(id, auth.CredentialId, enrolled.DeviceToken);
    }

    private static Task<Result<int>> PublishNoticeAsync(TestDatabase db, string text) =>
        SendAsync<PublishPrivacyNoticeCommand, Result<int>>(db, new PublishPrivacyNoticeCommand(text, Admin));

    private static Task<Result<Guid>> CreateOperatorAsync(TestDatabase db, string employeeNumber, string temporaryPin) =>
        SendAsync<CreateOperatorCommand, Result<Guid>>(db, new CreateOperatorCommand(employeeNumber, "Operador", Group.C, null, temporaryPin));

    private static async Task LoginFullyAsync(TestDatabase db, Device device, string employeeNumber, string pin, string newPin)
    {
        var start = await SendAsync<StartOperatorLoginCommand, Result<LoginOutcome>>(
            db, new StartOperatorLoginCommand(device.WorkstationId, device.CredentialId, employeeNumber, pin));
        if (start.Value is LoginOutcome.StepsRequired pending)
        {
            var complete = await SendAsync<CompleteOperatorLoginCommand, Result<LoginOutcome>>(
                db, new CompleteOperatorLoginCommand(device.WorkstationId, device.CredentialId, pending.Ticket, pending.Notice?.Version, newPin));
            Assert.IsType<LoginOutcome.Opened>(complete.Value);
        }
    }

    private static async Task<TResult> SendAsync<TCommand, TResult>(TestDatabase db, TCommand command)
    {
        await using var scope = db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().HandleAsync(command, Ct);
    }

    private static async Task<TResult> QueryAsync<TQuery, TResult>(TestDatabase db, TQuery query)
    {
        await using var scope = db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().HandleAsync(query, Ct);
    }
}
