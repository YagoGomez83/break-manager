using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Application.Tests.Fakes;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;

namespace BreakManager.Application.Tests.Identity;

public sealed class WorkstationHandlersTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly IdentityTestContext _ctx = new();

    [Fact]
    public async Task CreateWorkstation_AddsIt()
    {
        var handler = new CreateWorkstationHandler(_ctx.Workstations, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new CreateWorkstationCommand(7, 2, 3, "PC-07"), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        var workstation = Assert.Single(_ctx.Workstations.Items);
        Assert.Equal(result.Value, workstation.Id);
        Assert.Equal(7, workstation.Number);
    }

    [Fact]
    public async Task CreateWorkstation_WithADuplicateNumber_Fails()
    {
        _ctx.SeedWorkstation(7);
        var handler = new CreateWorkstationHandler(_ctx.Workstations, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new CreateWorkstationCommand(7, 1, 1, "OTRA"), IdentityTestContext.Ct);

        Assert.Equal(IdentityErrors.WorkstationNumberTaken, result.Error);
        Assert.Equal(0, _ctx.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateWorkstation_ChangesLocationAndHostname()
    {
        var workstation = _ctx.SeedWorkstation(7);
        var handler = new UpdateWorkstationHandler(_ctx.Workstations, _ctx.UnitOfWork);

        var result = await handler.HandleAsync(new UpdateWorkstationCommand(workstation.Id, 4, 5, "PC-NUEVA"), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("PC-NUEVA", workstation.Hostname);
        Assert.Equal(1, _ctx.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task GenerateEnrollmentCode_ReturnsThePlainCodeOnce_AndStoresTheHash()
    {
        var workstation = _ctx.SeedWorkstation();

        var result = await Generate(workstation.Id);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_ctx.EnrollmentCodes.Items);
        Assert.Equal(_ctx.Tokens.Hash(result.Value.Code), stored.CodeHash);
        Assert.Equal(_ctx.Now + _ctx.Parameters.EnrollmentCodeLifetime, result.Value.ExpiresAt);
        var evt = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.EnrollmentCodeGenerated));
        Assert.Equal(Admin, evt.ActorId);
        Assert.Equal(workstation.Id, evt.WorkstationId);
    }

    [Fact]
    public async Task GenerateEnrollmentCode_InvalidatesThePreviousPendingCode()
    {
        var workstation = _ctx.SeedWorkstation();
        var first = await Generate(workstation.Id);

        await Generate(workstation.Id);

        var firstCode = _ctx.EnrollmentCodes.Items.Single(c => c.CodeHash == _ctx.Tokens.Hash(first.Value.Code));
        Assert.Equal(_ctx.Now, firstCode.InvalidatedAt);
    }

    [Fact]
    public async Task GenerateEnrollmentCode_ForAMissingWorkstation_Fails()
    {
        var result = await Generate(Guid.NewGuid());

        Assert.Equal(IdentityErrors.WorkstationNotFound, result.Error);
    }

    [Fact]
    public async Task RedeemEnrollmentCode_IssuesADeviceToken_AndConsumesTheCode()
    {
        var workstation = _ctx.SeedWorkstation();
        var code = (await Generate(workstation.Id)).Value.Code;

        var result = await Redeem(code);

        Assert.True(result.IsSuccess);
        Assert.Equal(workstation.Id, result.Value.WorkstationId);
        Assert.Equal(workstation.Number, result.Value.WorkstationNumber);
        Assert.Equal(_ctx.Tokens.Hash(result.Value.DeviceToken), workstation.ActiveCredential!.TokenHash);
        Assert.Equal("10.0.0.7", workstation.ActiveCredential.LastSeenIp);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.WorkstationEnrolled));
    }

    [Fact]
    public async Task RedeemEnrollmentCode_AcceptsLowercaseAndSeparators()
    {
        var workstation = _ctx.SeedWorkstation();
        var code = (await Generate(workstation.Id)).Value.Code;
        var typed = string.Join('-', code.ToLowerInvariant().Chunk(3).Select(c => new string(c)));

        Assert.True((await Redeem($" {typed} ")).IsSuccess);
    }

    [Fact]
    public async Task RedeemEnrollmentCode_Twice_FailsTheSecondTime()
    {
        var workstation = _ctx.SeedWorkstation();
        var code = (await Generate(workstation.Id)).Value.Code;
        await Redeem(code);

        var second = await Redeem(code);

        Assert.Equal(IdentityErrors.InvalidEnrollmentCode, second.Error);
    }

    [Fact]
    public async Task RedeemEnrollmentCode_Expired_Fails()
    {
        var workstation = _ctx.SeedWorkstation();
        var code = (await Generate(workstation.Id)).Value.Code;
        _ctx.Time.Advance(_ctx.Parameters.EnrollmentCodeLifetime);

        var result = await Redeem(code);

        Assert.Equal(IdentityErrors.InvalidEnrollmentCode, result.Error);
        Assert.Null(workstation.ActiveCredential);
    }

    [Fact]
    public async Task RedeemEnrollmentCode_Unknown_Fails()
    {
        var result = await Redeem("NOEXISTE");

        Assert.Equal(IdentityErrors.InvalidEnrollmentCode, result.Error);
    }

    [Fact]
    public async Task RedeemEnrollmentCode_AgainForTheSameWorkstation_ReplacesTheToken()
    {
        var workstation = _ctx.SeedWorkstation();
        var first = await Redeem((await Generate(workstation.Id)).Value.Code);

        var second = await Redeem((await Generate(workstation.Id)).Value.Code);

        Assert.NotEqual(first.Value.DeviceToken, second.Value.DeviceToken);
        Assert.Equal(_ctx.Tokens.Hash(second.Value.DeviceToken), workstation.ActiveCredential!.TokenHash);
    }

    [Fact]
    public async Task RevokeWorkstation_RevokesTheToken_AndClosesTheOpenSessionAsRevocation()
    {
        var (workstation, _) = _ctx.SeedEnrolledWorkstation();
        await _ctx.Sessions.OpenAsync(workstation.Id, Guid.NewGuid(), IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);
        var handler = new RevokeWorkstationHandler(_ctx.Workstations, _ctx.Sessions, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new RevokeWorkstationCommand(workstation.Id, Admin), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.True(workstation.Revoked);
        Assert.Null(workstation.ActiveCredential);
        Assert.Equal(SessionEndReason.Revocation, _ctx.SessionRepository.Items.Single().EndReason);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.WorkstationRevoked));
    }

    private async Task<BreakManager.Domain.Common.Result<GeneratedEnrollmentCode>> Generate(Guid workstationId)
    {
        var handler = new GenerateEnrollmentCodeHandler(
            _ctx.Workstations, _ctx.EnrollmentCodes, _ctx.Tokens, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time, _ctx.ParametersOptions);
        return await handler.HandleAsync(new GenerateEnrollmentCodeCommand(workstationId, Admin), IdentityTestContext.Ct);
    }

    private async Task<BreakManager.Domain.Common.Result<EnrolledDevice>> Redeem(string code)
    {
        var handler = new RedeemEnrollmentCodeHandler(
            _ctx.Workstations, _ctx.EnrollmentCodes, _ctx.Tokens, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);
        return await handler.HandleAsync(new RedeemEnrollmentCodeCommand(code, "10.0.0.7"), IdentityTestContext.Ct);
    }
}
