using BreakManager.Application.Tests.Fakes;
using BreakManager.Domain.Presence;

namespace BreakManager.Application.Tests.Presence;

public sealed class WorkstationSessionServiceTests
{
    private static readonly Guid Workstation1 = Guid.NewGuid();
    private static readonly Guid Workstation2 = Guid.NewGuid();
    private static readonly Guid OperatorA = Guid.NewGuid();
    private static readonly Guid OperatorB = Guid.NewGuid();

    private readonly IdentityTestContext _ctx = new();

    [Fact]
    public async Task OpenAsync_ReturnsThePlainToken_AndStoresOnlyItsHash()
    {
        var opened = await _ctx.Sessions.OpenAsync(Workstation1, OperatorA, IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);

        var stored = Assert.Single(_ctx.SessionRepository.Items);
        Assert.Equal(opened.SessionId, stored.Id);
        Assert.Equal(_ctx.Tokens.Hash(opened.Token), stored.TokenHash);
        Assert.NotEqual(opened.Token, stored.TokenHash);
        Assert.Equal(_ctx.Now + _ctx.Parameters.AgentSessionLifetime, opened.ExpiresAt);
        Assert.Empty(opened.Closed);
    }

    [Fact]
    public async Task OpenAsync_ReportsTheSessionsItClosed()
    {
        var bOnWorkstation1 = await OpenAndSave(Workstation1, OperatorB);
        var aOnWorkstation2 = await OpenAndSave(Workstation2, OperatorA);

        var opened = await _ctx.Sessions.OpenAsync(Workstation1, OperatorA, IdentityTestContext.Ct);

        Assert.Contains(opened.Closed, c => c.SessionId == bOnWorkstation1 && c.Reason == SessionEndReason.Relief);
        Assert.Contains(opened.Closed, c => c.SessionId == aOnWorkstation2 && c.Reason == SessionEndReason.MovedToAnotherWorkstation);
    }

    [Fact]
    public async Task FindUsableByTokenAsync_FindsOpenUnexpiredSessions_Only()
    {
        var opened = await _ctx.Sessions.OpenAsync(Workstation1, OperatorA, IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);

        var found = await _ctx.Sessions.FindUsableByTokenAsync(opened.Token, IdentityTestContext.Ct);
        Assert.Equal(new BreakManager.Application.Presence.SessionInfo(opened.SessionId, Workstation1, OperatorA), found);

        Assert.Null(await _ctx.Sessions.FindUsableByTokenAsync("otro", IdentityTestContext.Ct));

        _ctx.Time.Advance(_ctx.Parameters.AgentSessionLifetime);
        Assert.Null(await _ctx.Sessions.FindUsableByTokenAsync(opened.Token, IdentityTestContext.Ct));
    }

    [Fact]
    public async Task CloseAsync_EndsTheSessionWithTheReason()
    {
        var sessionId = await OpenAndSave(Workstation1, OperatorA);

        var result = await _ctx.Sessions.CloseAsync(sessionId, SessionEndReason.Logout, IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        var session = Assert.Single(_ctx.SessionRepository.Items);
        Assert.Equal(SessionEndReason.Logout, session.EndReason);
    }

    [Fact]
    public async Task CloseOpenForWorkstationAsync_ClosesTheOpenSession_OrReturnsNull()
    {
        Assert.Null(await _ctx.Sessions.CloseOpenForWorkstationAsync(Workstation1, SessionEndReason.Revocation, IdentityTestContext.Ct));

        var sessionId = await OpenAndSave(Workstation1, OperatorA);

        var closed = await _ctx.Sessions.CloseOpenForWorkstationAsync(Workstation1, SessionEndReason.Revocation, IdentityTestContext.Ct);

        Assert.Equal(sessionId, closed);
        Assert.Equal(SessionEndReason.Revocation, _ctx.SessionRepository.Items.Single().EndReason);
    }

    private async Task<Guid> OpenAndSave(Guid workstationId, Guid operatorId)
    {
        var opened = await _ctx.Sessions.OpenAsync(workstationId, operatorId, IdentityTestContext.Ct);
        await _ctx.UnitOfWork.SaveChangesAsync(IdentityTestContext.Ct);
        return opened.SessionId;
    }
}
