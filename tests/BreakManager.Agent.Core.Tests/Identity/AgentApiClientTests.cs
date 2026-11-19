using System.Net;
using System.Net.Http.Json;
using BreakManager.Agent.Core.Identity;
using BreakManager.Contracts.Agent;

namespace BreakManager.Agent.Core.Tests.Identity;

public sealed class AgentApiClientTests
{
    private static readonly DeviceCredential Credential = new(new Uri("https://breakmanager.local"), "device-token", 7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartLogin_SendsTheDeviceToken_AndParsesTheResponse()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new LoginResponse(
            LoginStatus.Opened, new SessionDto(Guid.NewGuid(), "s", DateTimeOffset.UtcNow), null));
        var client = new AgentApiClient(new HttpClient(handler));

        var result = await client.StartLoginAsync(Credential, new StartLoginRequest("20481", "482913"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginStatus.Opened, result.Value!.Status);
        Assert.Equal(new Uri("https://breakmanager.local/api/agent/sessions"), handler.Request!.RequestUri);
        Assert.Equal("Device", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("device-token", handler.Request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Logout_SendsTheSessionToken()
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent, null);
        var client = new AgentApiClient(new HttpClient(handler));

        var result = await client.LogoutAsync(Credential, "session-token", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal("session-token", handler.Request.Headers.GetValues(AgentHeaders.SessionToken).Single());
    }

    [Fact]
    public async Task Errors_CarryTheStatusAndTheServerError()
    {
        var handler = new RecordingHandler(HttpStatusCode.Locked, new AgentError("Identity.OperatorLockedOut", "Legajo bloqueado."));
        var client = new AgentApiClient(new HttpClient(handler));

        var result = await client.StartLoginAsync(Credential, new StartLoginRequest("20481", "000001"), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(423, result.StatusCode);
        Assert.Equal("Legajo bloqueado.", result.Error!.Message);
    }

    [Fact]
    public async Task NetworkFailures_BecomeAFriendlyError()
    {
        var client = new AgentApiClient(new HttpClient(new ThrowingHandler()));

        var result = await client.EnrollAsync(new Uri("https://breakmanager.local"), new EnrollRequest("CODE"), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.StatusCode);
        Assert.Contains("servidor", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonHttpsServers_AreRejected()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, null);
        var client = new AgentApiClient(new HttpClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.EnrollAsync(new Uri("http://breakmanager.local"), new EnrollRequest("CODE"), Ct));
        Assert.Null(handler.Request);
    }

    private sealed class RecordingHandler(HttpStatusCode status, object? body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body, body.GetType());
            }

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("sin conexión");
    }
}

public sealed class PinRulesTests
{
    [Theory]
    [InlineData("482913", "20481", null)]
    [InlineData("12345", "20481", "6 dígitos")]
    [InlineData("12a456", "20481", "6 dígitos")]
    [InlineData("111111", "20481", "fácil")]
    [InlineData("654321", "20481", "fácil")]
    [InlineData("020481", "1020481", "fácil")]
    public void Check_FollowsTheServerRules(string pin, string employeeNumber, string? expectedFragment)
    {
        var message = PinRules.Check(pin, employeeNumber);

        if (expectedFragment is null)
        {
            Assert.Null(message);
        }
        else
        {
            Assert.Contains(expectedFragment, message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeviceCredential_ToString_DoesNotRevealTheToken()
    {
        Assert.DoesNotContain(
            "secreto",
            new DeviceCredential(new Uri("https://x"), "secreto", 1).ToString(),
            StringComparison.Ordinal);
    }
}
