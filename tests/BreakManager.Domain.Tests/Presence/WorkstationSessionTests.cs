using BreakManager.Domain.Presence;

namespace BreakManager.Domain.Tests.Presence;

public sealed class WorkstationSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private static readonly Guid Workstation1 = Guid.NewGuid();
    private static readonly Guid Workstation2 = Guid.NewGuid();
    private static readonly Guid OperatorA = Guid.NewGuid();
    private static readonly Guid OperatorB = Guid.NewGuid();

    [Fact]
    public void Open_WithNothingOpen_CreatesASession()
    {
        var opening = Open(onWorkstation: null, forOperator: null, Workstation1, OperatorA, "token");

        Assert.True(opening.IsNew);
        Assert.Empty(opening.Closed);
        Assert.Equal(Workstation1, opening.Session.WorkstationId);
        Assert.Equal(OperatorA, opening.Session.OperatorId);
        Assert.Equal("token", opening.Session.TokenHash);
        Assert.Equal(T0 + Lifetime, opening.Session.ExpiresAt);
        Assert.True(opening.Session.IsOpen);
    }

    [Fact]
    public void Open_OnAWorkstationTakenByAnotherOperator_ClosesItAsRelief()
    {
        var previous = Open(null, null, Workstation1, OperatorA, "a").Session;

        var opening = Open(onWorkstation: previous, forOperator: null, Workstation1, OperatorB, "b");

        Assert.True(opening.IsNew);
        Assert.Equal([previous], opening.Closed);
        Assert.Equal(SessionEndReason.Relief, previous.EndReason);
        Assert.Equal(T0, previous.EndedAt);
    }

    [Fact]
    public void Open_WhenTheOperatorIsOnAnotherWorkstation_ClosesItAsMovedToAnotherWorkstation()
    {
        var previous = Open(null, null, Workstation1, OperatorA, "a").Session;

        var opening = Open(onWorkstation: null, forOperator: previous, Workstation2, OperatorA, "a2");

        Assert.True(opening.IsNew);
        Assert.Equal(SessionEndReason.MovedToAnotherWorkstation, previous.EndReason);
    }

    [Fact]
    public void Open_ClosesBoth_WhenTheWorkstationIsTakenAndTheOperatorWasElsewhere()
    {
        var bOnWorkstation1 = Open(null, null, Workstation1, OperatorB, "b").Session;
        var aOnWorkstation2 = Open(null, null, Workstation2, OperatorA, "a").Session;

        var opening = Open(onWorkstation: bOnWorkstation1, forOperator: aOnWorkstation2, Workstation1, OperatorA, "a1");

        Assert.Equal(2, opening.Closed.Count);
        Assert.Equal(SessionEndReason.Relief, bOnWorkstation1.EndReason);
        Assert.Equal(SessionEndReason.MovedToAnotherWorkstation, aOnWorkstation2.EndReason);
    }

    [Fact]
    public void Open_SameOperatorOnTheSameWorkstation_RenewsTheTokenWithoutClosing()
    {
        var previous = Open(null, null, Workstation1, OperatorA, "old").Session;
        var later = T0.AddHours(1);

        var opening = WorkstationSession.Open(
            previous, previous, Guid.NewGuid(), Workstation1, OperatorA, "new", later, Lifetime);

        Assert.False(opening.IsNew);
        Assert.Same(previous, opening.Session);
        Assert.Empty(opening.Closed);
        Assert.True(previous.IsOpen);
        Assert.Equal("new", previous.TokenHash);
        Assert.Equal(later + Lifetime, previous.ExpiresAt);
        Assert.Equal(T0, previous.StartedAt);
    }

    [Fact]
    public void Open_RejectsSessionsThatDoNotMatchTheirRole()
    {
        var onWorkstation2 = Open(null, null, Workstation2, OperatorB, "b").Session;

        Assert.Throws<ArgumentException>(() => Open(onWorkstation: onWorkstation2, forOperator: null, Workstation1, OperatorA, "a"));
    }

    [Fact]
    public void End_ClosesWithTheReason_AndOnlyOnce()
    {
        var session = Open(null, null, Workstation1, OperatorA, "a").Session;

        var first = session.End(T0.AddMinutes(30), SessionEndReason.Logout);
        var second = session.End(T0.AddMinutes(31), SessionEndReason.Revocation);

        Assert.True(first.IsSuccess);
        Assert.Equal(WorkstationSessionErrors.AlreadyEnded, second.Error);
        Assert.Equal(SessionEndReason.Logout, session.EndReason);
        Assert.Equal(T0.AddMinutes(30), session.EndedAt);
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void IsUsable_RequiresAnOpenSessionWithinItsLifetime()
    {
        var session = Open(null, null, Workstation1, OperatorA, "a").Session;

        Assert.True(session.IsUsable(T0.AddHours(7)));
        Assert.False(session.IsUsable(T0 + Lifetime));

        session.End(T0.AddHours(1), SessionEndReason.Logout);
        Assert.False(session.IsUsable(T0.AddHours(2)));
    }

    private static SessionOpening Open(
        WorkstationSession? onWorkstation,
        WorkstationSession? forOperator,
        Guid workstationId,
        Guid operatorId,
        string tokenHash) =>
        WorkstationSession.Open(onWorkstation, forOperator, Guid.NewGuid(), workstationId, operatorId, tokenHash, T0, Lifetime);
}
