using BreakManager.Agent.Core.Identity;
using BreakManager.Contracts.Agent;

namespace BreakManager.Agent.Core.Tests.Identity;

public sealed class LoginFlowTests
{
    private static readonly Uri Server = new("https://breakmanager.local");
    private static readonly DeviceCredential Credential = new(Server, "device-token", 7);
    private static readonly PrivacyNoticeDto Notice = new(1, "Texto del aviso");

    private readonly FakeAgentApi _api = new();
    private readonly InMemoryCredentialStore _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Initialize_WithoutCredential_AsksForEnrollment()
    {
        var flow = await StartAsync();

        Assert.Equal(LoginStep.NotEnrolled, flow.Step);
    }

    [Fact]
    public async Task Initialize_WithCredential_ShowsLogin()
    {
        _store.Credential = Credential;

        var flow = await StartAsync();

        Assert.Equal(LoginStep.Login, flow.Step);
        Assert.Equal(7, flow.WorkstationNumber);
    }

    [Fact]
    public async Task Enroll_StoresTheCredential_AndShowsLogin()
    {
        var flow = await StartAsync();
        _api.EnrollResult = AgentApiResult<EnrollResponse>.Ok(new EnrollResponse(Guid.NewGuid(), 7, "nuevo-token"));

        await flow.EnrollAsync("https://breakmanager.local", "ABCD-EFGH-JK", Ct);

        Assert.Equal(LoginStep.Login, flow.Step);
        Assert.Equal(new DeviceCredential(Server, "nuevo-token", 7), _store.Credential);
        Assert.Null(flow.ErrorMessage);
    }

    [Theory]
    [InlineData("http://breakmanager.local")]
    [InlineData("no es una url")]
    public async Task Enroll_RequiresAnHttpsServerUrl(string url)
    {
        var flow = await StartAsync();

        await flow.EnrollAsync(url, "CODE", Ct);

        Assert.Equal(LoginStep.NotEnrolled, flow.Step);
        Assert.NotNull(flow.ErrorMessage);
        Assert.Equal(0, _api.EnrollCalls);
    }

    [Fact]
    public async Task Enroll_WithAnInvalidCode_ShowsTheServerMessage()
    {
        var flow = await StartAsync();
        _api.EnrollResult = AgentApiResult<EnrollResponse>.Fail(400, new AgentError("Identity.InvalidEnrollmentCode", "Código inválido."));

        await flow.EnrollAsync("https://breakmanager.local", "MAL", Ct);

        Assert.Equal(LoginStep.NotEnrolled, flow.Step);
        Assert.Equal("Código inválido.", flow.ErrorMessage);
        Assert.Null(_store.Credential);
    }

    [Fact]
    public async Task Login_WithNothingPending_OpensTheSession()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Opened();

        await flow.LoginAsync("20481", "482913", Ct);

        Assert.Equal(LoginStep.SessionOpen, flow.Step);
    }

    [Fact]
    public async Task FirstLogin_GoesThroughNotice_ThenPinChange_ThenSession()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Steps(Notice, requiresPinChange: true);
        _api.CompleteResult = Opened();

        await flow.LoginAsync("20481", "482913", Ct);
        Assert.Equal(LoginStep.NoticePending, flow.Step);
        Assert.Equal(Notice, flow.Notice);

        await flow.AcceptNoticeAsync(Ct);
        Assert.Equal(LoginStep.PinChangePending, flow.Step);
        Assert.Equal(0, _api.CompleteCalls);

        await flow.ChangePinAsync("736251", "736251", Ct);

        Assert.Equal(LoginStep.SessionOpen, flow.Step);
        Assert.Equal(new CompleteLoginRequest("ticket", 1, "736251"), _api.LastComplete);
    }

    [Fact]
    public async Task NoticeOnly_CompletesRightAfterAccepting()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Steps(Notice, requiresPinChange: false);
        _api.CompleteResult = Opened();

        await flow.LoginAsync("20481", "482913", Ct);
        await flow.AcceptNoticeAsync(Ct);

        Assert.Equal(LoginStep.SessionOpen, flow.Step);
        Assert.Equal(new CompleteLoginRequest("ticket", 1, null), _api.LastComplete);
    }

    [Fact]
    public async Task RejectingTheNotice_GoesBackToLogin()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Steps(Notice, requiresPinChange: true);
        await flow.LoginAsync("20481", "482913", Ct);

        flow.RejectNotice();

        Assert.Equal(LoginStep.Login, flow.Step);
        Assert.Null(flow.Notice);
        Assert.Equal(0, _api.CompleteCalls);
    }

    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("123456", "123456")]
    [InlineData("736251", "736252")]
    [InlineData("020481", "020481")]
    public async Task ChangePin_ValidatesLocallyBeforeCallingTheServer(string pin, string confirmation)
    {
        var flow = await ReadyAsync();
        _api.StartResult = Steps(notice: null, requiresPinChange: true);
        await flow.LoginAsync("1020481", "482913", Ct);

        await flow.ChangePinAsync(pin, confirmation, Ct);

        Assert.Equal(LoginStep.PinChangePending, flow.Step);
        Assert.NotNull(flow.ErrorMessage);
        Assert.Equal(0, _api.CompleteCalls);
    }

    [Fact]
    public async Task Complete_WhenANewNoticeWasPublished_AsksForItAgain()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Steps(Notice, requiresPinChange: false);
        var newer = new PrivacyNoticeDto(2, "Aviso nuevo");
        _api.CompleteResult = AgentApiResult<LoginResponse>.Ok(
            new LoginResponse(LoginStatus.StepsRequired, null, new PendingStepsDto("ticket-2", DateTimeOffset.UtcNow, newer, false)));
        await flow.LoginAsync("20481", "482913", Ct);

        await flow.AcceptNoticeAsync(Ct);

        Assert.Equal(LoginStep.NoticePending, flow.Step);
        Assert.Equal(newer, flow.Notice);
    }

    [Fact]
    public async Task Login_WithWrongCredentials_StaysOnLogin_WithTheMessage()
    {
        var flow = await ReadyAsync();
        _api.StartResult = AgentApiResult<LoginResponse>.Fail(400, new AgentError("Identity.InvalidCredentials", "Legajo o PIN incorrectos."));

        await flow.LoginAsync("20481", "000001", Ct);

        Assert.Equal(LoginStep.Login, flow.Step);
        Assert.Equal("Legajo o PIN incorrectos.", flow.ErrorMessage);
    }

    [Fact]
    public async Task UnauthorizedDevice_ForgetsTheCredential_AndAsksForEnrollment()
    {
        var flow = await ReadyAsync();
        _api.StartResult = AgentApiResult<LoginResponse>.Fail(401, new AgentError("Identity.Unauthorized", "No autorizado."));

        await flow.LoginAsync("20481", "482913", Ct);

        Assert.Equal(LoginStep.NotEnrolled, flow.Step);
        Assert.Null(_store.Credential);
        Assert.NotNull(flow.ErrorMessage);
    }

    [Fact]
    public async Task Logout_ClosesTheSession_AndShowsLogin()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Opened();
        await flow.LoginAsync("20481", "482913", Ct);

        await flow.LogoutAsync(Ct);

        Assert.Equal(LoginStep.Login, flow.Step);
        Assert.Equal("session-token", _api.LastLogoutSessionToken);
    }

    [Fact]
    public async Task Changed_IsRaisedOnEveryStep()
    {
        var flow = await ReadyAsync();
        _api.StartResult = Opened();
        var raised = 0;
        flow.Changed += (_, _) => raised++;

        await flow.LoginAsync("20481", "482913", Ct);

        Assert.True(raised > 0);
    }

    private async Task<LoginFlow> StartAsync()
    {
        var flow = new LoginFlow(_api, _store);
        await flow.InitializeAsync(Ct);
        return flow;
    }

    private async Task<LoginFlow> ReadyAsync()
    {
        _store.Credential = Credential;
        return await StartAsync();
    }

    private static AgentApiResult<LoginResponse> Opened() =>
        AgentApiResult<LoginResponse>.Ok(new LoginResponse(
            LoginStatus.Opened, new SessionDto(Guid.NewGuid(), "session-token", DateTimeOffset.UtcNow.AddHours(8)), null));

    private static AgentApiResult<LoginResponse> Steps(PrivacyNoticeDto? notice, bool requiresPinChange) =>
        AgentApiResult<LoginResponse>.Ok(new LoginResponse(
            LoginStatus.StepsRequired, null, new PendingStepsDto("ticket", DateTimeOffset.UtcNow.AddMinutes(5), notice, requiresPinChange)));
}

internal sealed class InMemoryCredentialStore : IDeviceCredentialStore
{
    public DeviceCredential? Credential { get; set; }

    public Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Credential);

    public Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        Credential = credential;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        Credential = null;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAgentApi : IAgentApi
{
    public AgentApiResult<EnrollResponse> EnrollResult { get; set; } =
        AgentApiResult<EnrollResponse>.Fail(500, new AgentError("NotSet", "No configurado."));

    public AgentApiResult<LoginResponse> StartResult { get; set; } =
        AgentApiResult<LoginResponse>.Fail(500, new AgentError("NotSet", "No configurado."));

    public AgentApiResult<LoginResponse> CompleteResult { get; set; } =
        AgentApiResult<LoginResponse>.Fail(500, new AgentError("NotSet", "No configurado."));

    public int EnrollCalls { get; private set; }

    public int CompleteCalls { get; private set; }

    public CompleteLoginRequest? LastComplete { get; private set; }

    public string? LastLogoutSessionToken { get; private set; }

    public Task<AgentApiResult<EnrollResponse>> EnrollAsync(Uri server, EnrollRequest request, CancellationToken cancellationToken)
    {
        EnrollCalls++;
        return Task.FromResult(EnrollResult);
    }

    public Task<AgentApiResult<LoginResponse>> StartLoginAsync(DeviceCredential credential, StartLoginRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(StartResult);

    public Task<AgentApiResult<LoginResponse>> CompleteLoginAsync(DeviceCredential credential, CompleteLoginRequest request, CancellationToken cancellationToken)
    {
        CompleteCalls++;
        LastComplete = request;
        return Task.FromResult(CompleteResult);
    }

    public Task<AgentApiResult<bool>> LogoutAsync(DeviceCredential credential, string sessionToken, CancellationToken cancellationToken)
    {
        LastLogoutSessionToken = sessionToken;
        return Task.FromResult(AgentApiResult<bool>.Ok(true));
    }
}
