using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class LoginTicketTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly Guid Device = Guid.NewGuid();

    [Fact]
    public void Issue_KeepsTheBindingsAndTheRequiredSteps()
    {
        var operatorId = Guid.NewGuid();

        var ticket = LoginTicket.Issue(Guid.NewGuid(), operatorId, Device, "hash", noticeVersion: 2, requiresPinChange: true, T0, Lifetime);

        Assert.Equal(operatorId, ticket.OperatorId);
        Assert.Equal(Device, ticket.DeviceCredentialId);
        Assert.Equal(2, ticket.NoticeVersion);
        Assert.True(ticket.RequiresPinChange);
        Assert.Equal(T0 + Lifetime, ticket.ExpiresAt);
    }

    [Fact]
    public void Consume_FromTheSameDevice_WithTheRequestedVersion_Succeeds_Once()
    {
        var ticket = NewTicket(noticeVersion: 2);

        var first = ticket.Consume(T0.AddMinutes(1), Device, acceptedNoticeVersion: 2);
        var second = ticket.Consume(T0.AddMinutes(2), Device, acceptedNoticeVersion: 2);

        Assert.True(first.IsSuccess);
        Assert.Equal(T0.AddMinutes(1), ticket.ConsumedAt);
        Assert.Equal(LoginTicketErrors.AlreadyUsed, second.Error);
    }

    [Fact]
    public void Consume_FromAnotherDevice_FailsAndIsNotConsumed()
    {
        var ticket = NewTicket(noticeVersion: 2);

        var result = ticket.Consume(T0.AddMinutes(1), Guid.NewGuid(), acceptedNoticeVersion: 2);

        Assert.Equal(LoginTicketErrors.WrongDevice, result.Error);
        Assert.Null(ticket.ConsumedAt);
    }

    [Fact]
    public void Consume_AtOrAfterExpiration_Fails()
    {
        var ticket = NewTicket(noticeVersion: null);

        var result = ticket.Consume(T0 + Lifetime, Device, acceptedNoticeVersion: null);

        Assert.Equal(LoginTicketErrors.Expired, result.Error);
        Assert.Null(ticket.ConsumedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(3)]
    public void Consume_WithoutAcceptingTheRequestedVersion_Fails(int? accepted)
    {
        var ticket = NewTicket(noticeVersion: 2);

        var result = ticket.Consume(T0.AddMinutes(1), Device, accepted);

        Assert.Equal(LoginTicketErrors.NoticeVersionMismatch, result.Error);
        Assert.Null(ticket.ConsumedAt);
    }

    [Fact]
    public void Consume_WhenNoNoticeWasRequested_IgnoresNoAcceptance()
    {
        var ticket = NewTicket(noticeVersion: null);

        Assert.True(ticket.Consume(T0.AddMinutes(1), Device, acceptedNoticeVersion: null).IsSuccess);
    }

    private static LoginTicket NewTicket(int? noticeVersion) =>
        LoginTicket.Issue(Guid.NewGuid(), Guid.NewGuid(), Device, "hash", noticeVersion, requiresPinChange: false, T0, Lifetime);
}
