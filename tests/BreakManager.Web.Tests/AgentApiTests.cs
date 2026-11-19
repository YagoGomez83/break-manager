using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BreakManager.Application.Identity.Administration;
using BreakManager.Application.Identity.Queries;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Contracts.Agent;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Web.Tests.Infrastructure;

namespace BreakManager.Web.Tests;

public sealed class AgentApiTests(WebTestHost host) : IClassFixture<WebTestHost>, IAsyncLifetime
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static int s_nextWorkstation;
    private static int s_nextEmployee = 50000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Un aviso publicado para toda la clase (sin aviso no se puede abrir sesión; eso se prueba aparte).
        if (await host.CountAsync("SELECT count(*) FROM privacy_notices") == 0)
        {
            await host.SendAsync<PublishPrivacyNoticeCommand, Result<int>>(new PublishPrivacyNoticeCommand("Aviso de prueba", Admin));
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Enrollment_IssuesADeviceToken_AndTheCodeCannotBeReused()
    {
        var (workstationId, code) = await NewWorkstationWithCodeAsync();
        using var client = host.CreateClient();

        var first = await client.PostAsJsonAsync(AgentApiRoutes.Enrollment, new EnrollRequest(code), Ct);
        var second = await client.PostAsJsonAsync(AgentApiRoutes.Enrollment, new EnrollRequest(code), Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var enrolled = await first.Content.ReadFromJsonAsync<EnrollResponse>(Ct);
        Assert.Equal(workstationId, enrolled!.WorkstationId);
        Assert.False(string.IsNullOrEmpty(enrolled.DeviceToken));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("Identity.InvalidEnrollmentCode", (await second.Content.ReadFromJsonAsync<AgentError>(Ct))!.Code);
    }

    [Fact]
    public async Task AgentEndpoints_WithoutAValidDeviceToken_Return401()
    {
        using var client = host.CreateClient();

        var missing = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest("1", "482913"), Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AgentHeaders.DeviceScheme, "inventado");
        var invalid = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest("1", "482913"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task RevokedWorkstation_Gets401()
    {
        var device = await EnrollAsync();
        await host.SendAsync<RevokeWorkstationCommand, Result>(new RevokeWorkstationCommand(device.WorkstationId, Admin));
        using var client = AgentClient(device.Token);

        var response = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest("1", "482913"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FirstLogin_NoticeAndPinChange_OpensASessionVisibleToTheAdmin_AndLogoutClosesIt()
    {
        var device = await EnrollAsync();
        var employee = await NewOperatorAsync("482913");
        using var client = AgentClient(device.Token);

        var start = await ReadLoginAsync(await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest(employee, "482913"), Ct));
        Assert.Equal(LoginStatus.StepsRequired, start.Status);
        Assert.True(start.Steps!.RequiresPinChange);
        Assert.NotNull(start.Steps.Notice);

        var complete = await ReadLoginAsync(await client.PostAsJsonAsync(
            AgentApiRoutes.SessionCompletion,
            new CompleteLoginRequest(start.Steps.Ticket, start.Steps.Notice!.Version, "736251"),
            Ct));
        Assert.Equal(LoginStatus.Opened, complete.Status);

        var open = await host.QueryAsync<ListOpenSessionsQuery, IReadOnlyList<OpenSessionListItem>>(new ListOpenSessionsQuery());
        Assert.Contains(open, s => s.EmployeeNumber == employee && s.WorkstationNumber == device.Number);

        client.DefaultRequestHeaders.Add(AgentHeaders.SessionToken, complete.Session!.SessionToken);
        var logout = await client.DeleteAsync(new Uri(AgentApiRoutes.CurrentSession, UriKind.Relative), Ct);
        var again = await client.DeleteAsync(new Uri(AgentApiRoutes.CurrentSession, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task TicketUsedFromAnotherDevice_IsRejected()
    {
        var device = await EnrollAsync();
        var other = await EnrollAsync();
        var employee = await NewOperatorAsync("482913");
        using var client = AgentClient(device.Token);
        using var otherClient = AgentClient(other.Token);
        var start = await ReadLoginAsync(await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest(employee, "482913"), Ct));

        var response = await otherClient.PostAsJsonAsync(
            AgentApiRoutes.SessionCompletion,
            new CompleteLoginRequest(start.Steps!.Ticket, start.Steps.Notice?.Version, "736251"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidLoginTicket", (await response.Content.ReadFromJsonAsync<AgentError>(Ct))!.Code);

        var retry = await client.PostAsJsonAsync(
            AgentApiRoutes.SessionCompletion,
            new CompleteLoginRequest(start.Steps.Ticket, start.Steps.Notice?.Version, "736251"),
            Ct);
        Assert.Equal(LoginStatus.Opened, (await ReadLoginAsync(retry)).Status);
    }

    [Fact]
    public async Task WrongPins_LockTheEmployeeNumber_With423()
    {
        var device = await EnrollAsync();
        var employee = await NewOperatorAsync("482913");
        using var client = AgentClient(device.Token);

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest(employee, "000001"), Ct);
        }

        Assert.Equal(HttpStatusCode.Locked, last.StatusCode);
        var withRightPin = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest(employee, "482913"), Ct);
        Assert.Equal(HttpStatusCode.Locked, withRightPin.StatusCode);
    }

    [Fact]
    public async Task AnotherOperatorOnTheSameWorkstation_ClosesThePreviousSessionAsRelief()
    {
        var device = await EnrollAsync();
        var first = await NewOperatorAsync("482913");
        var second = await NewOperatorAsync("918273");
        using var client = AgentClient(device.Token);
        await LoginFullyAsync(client, first, "482913");
        var before = await host.CountAsync("SELECT count(*) FROM workstation_sessions WHERE end_reason = 'Relief'");

        await LoginFullyAsync(client, second, "918273");

        Assert.Equal(before + 1, await host.CountAsync("SELECT count(*) FROM workstation_sessions WHERE end_reason = 'Relief'"));
    }

    private sealed record Device(Guid WorkstationId, int Number, string Token);

    private HttpClient AgentClient(string deviceToken)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AgentHeaders.DeviceScheme, deviceToken);
        return client;
    }

    private async Task<(Guid WorkstationId, string Code)> NewWorkstationWithCodeAsync()
    {
        var number = Interlocked.Increment(ref s_nextWorkstation);
        var id = (await host.SendAsync<CreateWorkstationCommand, Result<Guid>>(new CreateWorkstationCommand(number, 1, number, $"PC-{number:00}"))).Value;
        var code = (await host.SendAsync<GenerateEnrollmentCodeCommand, Result<GeneratedEnrollmentCode>>(new GenerateEnrollmentCodeCommand(id, Admin))).Value;
        return (id, code.Code);
    }

    private async Task<Device> EnrollAsync()
    {
        var (workstationId, code) = await NewWorkstationWithCodeAsync();
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync(AgentApiRoutes.Enrollment, new EnrollRequest(code), Ct);
        var enrolled = (await response.Content.ReadFromJsonAsync<EnrollResponse>(Ct))!;
        return new Device(workstationId, enrolled.WorkstationNumber, enrolled.DeviceToken);
    }

    private async Task<string> NewOperatorAsync(string temporaryPin)
    {
        var employee = Interlocked.Increment(ref s_nextEmployee).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await host.SendAsync<CreateOperatorCommand, Result<Guid>>(new CreateOperatorCommand(employee, "Operador", Group.C, null, temporaryPin));
        return employee;
    }

    private static async Task LoginFullyAsync(HttpClient client, string employee, string pin)
    {
        var start = await ReadLoginAsync(await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest(employee, pin), Ct));
        if (start.Status == LoginStatus.StepsRequired)
        {
            var complete = await ReadLoginAsync(await client.PostAsJsonAsync(
                AgentApiRoutes.SessionCompletion,
                new CompleteLoginRequest(start.Steps!.Ticket, start.Steps.Notice?.Version, "647382"),
                Ct));
            Assert.Equal(LoginStatus.Opened, complete.Status);
        }
    }

    private static async Task<LoginResponse> ReadLoginAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(Ct))!;
    }
}

public sealed class AgentApiWithoutNoticeTests(WebTestHost host) : IClassFixture<WebTestHost>
{
    [Fact]
    public async Task Login_WithoutAPublishedNotice_IsNotAllowed()
    {
        var admin = Guid.NewGuid();
        var id = (await host.SendAsync<CreateWorkstationCommand, Result<Guid>>(new CreateWorkstationCommand(1, 1, 1, "PC-01"))).Value;
        var code = (await host.SendAsync<GenerateEnrollmentCodeCommand, Result<GeneratedEnrollmentCode>>(new GenerateEnrollmentCodeCommand(id, admin))).Value;
        await host.SendAsync<CreateOperatorCommand, Result<Guid>>(new CreateOperatorCommand("40000", "Operador", Group.C, null, "482913"));
        using var client = host.CreateClient();
        var enrolled = (await (await client.PostAsJsonAsync(AgentApiRoutes.Enrollment, new EnrollRequest(code.Code), TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<EnrollResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AgentHeaders.DeviceScheme, enrolled.DeviceToken);

        var response = await client.PostAsJsonAsync(AgentApiRoutes.Sessions, new StartLoginRequest("40000", "482913"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "PrivacyNotice.NonePublished",
            (await response.Content.ReadFromJsonAsync<AgentError>(TestContext.Current.CancellationToken))!.Code);
    }
}
