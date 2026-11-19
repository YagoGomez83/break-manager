using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Application.Tests.Fakes;
using BreakManager.Domain.Common;

namespace BreakManager.Application.Tests.Identity;

public sealed class AuthenticateDeviceTests
{
    private const string DeviceToken = "device-token";

    private readonly IdentityTestContext _ctx = new();

    [Fact]
    public async Task ValidToken_IdentifiesTheWorkstation_AndRecordsLastSeen()
    {
        var (workstation, credential) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);

        var result = await Authenticate(DeviceToken, ip: "10.0.0.7");

        Assert.True(result.IsSuccess);
        Assert.Equal(workstation.Id, result.Value.WorkstationId);
        Assert.Equal(credential.Id, result.Value.CredentialId);
        Assert.Null(result.Value.Session);
        Assert.Equal(_ctx.Now, credential.LastSeenAt);
        Assert.Equal("10.0.0.7", credential.LastSeenIp);
        Assert.Equal(1, _ctx.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task RepeatedRequestsWithinAMinute_DoNotWrite()
    {
        _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        await Authenticate(DeviceToken);
        _ctx.Time.Advance(TimeSpan.FromSeconds(30));

        await Authenticate(DeviceToken);

        Assert.Equal(1, _ctx.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task IpChange_RecordsDeviceTokenIpChanged_WithBothIps()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        await Authenticate(DeviceToken, ip: "10.0.0.7");

        await Authenticate(DeviceToken, ip: "10.0.0.99");

        var evt = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.DeviceTokenIpChanged));
        Assert.Equal(workstation.Id, evt.WorkstationId);
        Assert.Equal("10.0.0.7", evt.Data!["previousIp"]);
        Assert.Equal("10.0.0.99", evt.Data!["ip"]);
    }

    [Fact]
    public async Task UnknownToken_Fails_AndRecordsInvalidDeviceToken()
    {
        var result = await Authenticate("desconocido");

        Assert.Equal(IdentityErrors.InvalidDeviceToken, result.Error);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.InvalidDeviceToken));
    }

    [Fact]
    public async Task TokenOfARevokedWorkstation_Fails()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        workstation.Revoke(_ctx.Now);

        var result = await Authenticate(DeviceToken);

        Assert.Equal(IdentityErrors.InvalidDeviceToken, result.Error);
        Assert.Equal(workstation.Id, Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.InvalidDeviceToken)).WorkstationId);
    }

    [Fact]
    public async Task ReplacedToken_Fails()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        workstation.Enroll(Guid.NewGuid(), _ctx.Tokens.Hash("nuevo"), _ctx.Now);

        Assert.Equal(IdentityErrors.InvalidDeviceToken, (await Authenticate(DeviceToken)).Error);
        Assert.True((await Authenticate("nuevo")).IsSuccess);
    }

    [Fact]
    public async Task WithAValidSessionToken_IncludesTheSession()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        var operatorId = Guid.NewGuid();
        var opened = await _ctx.Sessions.OpenAsync(workstation.Id, operatorId, IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);

        var result = await Authenticate(DeviceToken, sessionToken: opened.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(opened.SessionId, result.Value.Session!.SessionId);
        Assert.Equal(operatorId, result.Value.Session.OperatorId);
    }

    [Fact]
    public async Task WithASessionOfAnotherWorkstation_Fails()
    {
        _ctx.SeedEnrolledWorkstation(number: 7, deviceToken: DeviceToken);
        var (other, _) = _ctx.SeedEnrolledWorkstation(number: 8, deviceToken: "otro");
        var opened = await _ctx.Sessions.OpenAsync(other.Id, Guid.NewGuid(), IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);

        var result = await Authenticate(DeviceToken, sessionToken: opened.Token);

        Assert.Equal(IdentityErrors.InvalidSession, result.Error);
    }

    [Fact]
    public async Task WithAnExpiredSessionToken_Fails()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation(deviceToken: DeviceToken);
        var opened = await _ctx.Sessions.OpenAsync(workstation.Id, Guid.NewGuid(), IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);
        _ctx.Time.Advance(_ctx.Parameters.AgentSessionLifetime);

        Assert.Equal(IdentityErrors.InvalidSession, (await Authenticate(DeviceToken, sessionToken: opened.Token)).Error);
    }

    private Task<Result<AuthenticatedDevice>> Authenticate(string deviceToken, string? sessionToken = null, string ip = "10.0.0.7")
    {
        var handler = new AuthenticateDeviceHandler(_ctx.Workstations, _ctx.Sessions, _ctx.Tokens, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);
        return handler.HandleAsync(new AuthenticateDeviceCommand(deviceToken, sessionToken, ip), IdentityTestContext.Ct);
    }
}
