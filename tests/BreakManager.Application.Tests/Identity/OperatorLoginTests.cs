using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Login;
using BreakManager.Application.Tests.Fakes;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;

namespace BreakManager.Application.Tests.Identity;

public sealed class OperatorLoginTests
{
    private const string EmployeeNo = "20481";
    private const string CurrentPin = "482913";
    private const string NewPin = "736251";

    private readonly IdentityTestContext _ctx = new();
    private readonly Workstation _workstation;
    private readonly DeviceCredential _credential;

    public OperatorLoginTests()
    {
        (_workstation, _credential) = _ctx.SeedEnrolledWorkstation();
    }

    // --- Inicio del login -------------------------------------------------------------------

    [Fact]
    public async Task Start_WithoutAPublishedNotice_Fails()
    {
        _ctx.SeedOperator(EmployeeNo, CurrentPin);

        var result = await Start(EmployeeNo, CurrentPin);

        Assert.Equal(PrivacyNoticeErrors.NonePublished, result.Error);
    }

    [Fact]
    public async Task Start_WithEverythingUpToDate_OpensTheSession()
    {
        var op = SeedReadyOperator();

        var result = await Start(EmployeeNo, CurrentPin);

        Assert.True(result.IsSuccess);
        var opened = Assert.IsType<LoginOutcome.Opened>(result.Value);
        Assert.Equal(op.Id, opened.OperatorId);
        var session = Assert.Single(_ctx.SessionRepository.Items);
        Assert.Equal(_ctx.Tokens.Hash(opened.SessionToken), session.TokenHash);
        Assert.Equal(_workstation.Id, session.WorkstationId);
    }

    [Theory]
    [InlineData("99999", CurrentPin)]
    [InlineData("abc", CurrentPin)]
    [InlineData(EmployeeNo, "000001")]
    public async Task Start_WithInvalidCredentials_FailsWithAGenericError(string employeeNumber, string pin)
    {
        SeedReadyOperator();

        var result = await Start(employeeNumber, pin);

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Empty(_ctx.SessionRepository.Items);
    }

    [Fact]
    public async Task Start_WithAWrongPin_RecordsTheFailureWithTheWorkstation()
    {
        var op = SeedReadyOperator();

        await Start(EmployeeNo, "000001");

        var evt = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.OperatorLoginFailed));
        Assert.Equal(op.Id, evt.ActorId);
        Assert.Equal(_workstation.Id, evt.WorkstationId);
    }

    [Fact]
    public async Task Start_ForAnInactiveOperator_FailsLikeInvalidCredentials()
    {
        var op = SeedReadyOperator();
        op.Deactivate();

        Assert.Equal(IdentityErrors.InvalidCredentials, (await Start(EmployeeNo, CurrentPin)).Error);
    }

    [Fact]
    public async Task Start_FifthWrongPin_LocksTheEmployeeNumber()
    {
        SeedReadyOperator();

        for (var i = 0; i < 4; i++)
        {
            await Start(EmployeeNo, "000001");
        }

        var fifth = await Start(EmployeeNo, "000001");

        Assert.Equal(IdentityErrors.OperatorLockedOut, fifth.Error);
        Assert.Equal(5, _ctx.SecurityEvents.OfType(SecurityEventType.OperatorLoginFailed).Count());
        var locked = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.OperatorLockedOut));
        Assert.Equal("false", locked.Data!["requiresManualUnlock"]);
        Assert.Equal(IdentityErrors.OperatorLockedOut, (await Start(EmployeeNo, CurrentPin)).Error);

        _ctx.Time.Advance(_ctx.Parameters.OperatorLockoutDuration);
        Assert.True((await Start(EmployeeNo, CurrentPin)).IsSuccess);
    }

    [Fact]
    public async Task Start_ThirdLockoutWithin24Hours_RequiresManualUnlock()
    {
        SeedReadyOperator();

        for (var lockout = 0; lockout < 3; lockout++)
        {
            for (var i = 0; i < 5; i++)
            {
                await Start(EmployeeNo, "000001");
            }

            _ctx.Time.Advance(_ctx.Parameters.OperatorLockoutDuration);
        }

        Assert.Equal(IdentityErrors.OperatorLockedOut, (await Start(EmployeeNo, CurrentPin)).Error);
        var last = _ctx.SecurityEvents.OfType(SecurityEventType.OperatorLockedOut).Last();
        Assert.Equal("true", last.Data!["requiresManualUnlock"]);
    }

    [Fact]
    public async Task Start_UsesTheConfiguredLockoutValues()
    {
        _ctx.Parameters.OperatorLockoutAttempts = 2;
        SeedReadyOperator();

        await Start(EmployeeNo, "000001");

        Assert.Equal(IdentityErrors.OperatorLockedOut, (await Start(EmployeeNo, "000001")).Error);
    }

    [Fact]
    public async Task Start_WithAPendingNotice_ReturnsStepsWithATicket_AndDoesNotOpenASession()
    {
        var notice = _ctx.SeedNotice("Texto del aviso");
        _ctx.SeedOperator(EmployeeNo, CurrentPin);

        var result = await Start(EmployeeNo, CurrentPin);

        var pending = Assert.IsType<LoginOutcome.StepsRequired>(result.Value);
        Assert.Equal(notice.Version, pending.Notice!.Version);
        Assert.Equal("Texto del aviso", pending.Notice.Text);
        Assert.False(pending.RequiresPinChange);
        Assert.Empty(_ctx.SessionRepository.Items);

        var ticket = Assert.Single(_ctx.LoginTickets.Items);
        Assert.Equal(_ctx.Tokens.Hash(pending.Ticket), ticket.TokenHash);
        Assert.Equal(_credential.Id, ticket.DeviceCredentialId);
        Assert.Equal(notice.Version, ticket.NoticeVersion);
    }

    [Fact]
    public async Task Start_WithATemporaryPin_RequiresChangingIt()
    {
        var notice = _ctx.SeedNotice();
        var op = _ctx.SeedOperator(EmployeeNo, CurrentPin, mustChangePin: true);
        _ctx.SeedAcceptance(op, notice, _workstation.Id);

        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);

        Assert.True(pending.RequiresPinChange);
        Assert.Null(pending.Notice);
    }

    [Fact]
    public async Task Start_OnAWorkstationTakenByAnotherOperator_ClosesItAsRelief()
    {
        var notice = _ctx.SeedNotice();
        var other = _ctx.SeedOperator("30000", "918273");
        _ctx.SeedAcceptance(other, notice, _workstation.Id);
        SeedReadyOperator(notice);
        await Start("30000", "918273");

        await Start(EmployeeNo, CurrentPin);

        var previous = _ctx.SessionRepository.Items.Single(s => s.OperatorId == other.Id);
        Assert.Equal(SessionEndReason.Relief, previous.EndReason);
    }

    [Fact]
    public async Task Start_OnAnotherWorkstation_ClosesThePreviousSessionAsMovedToAnotherWorkstation()
    {
        SeedReadyOperator();
        var (secondWorkstation, secondCredential) = _ctx.SeedEnrolledWorkstation(number: 8);
        await Start(EmployeeNo, CurrentPin);

        await Start(EmployeeNo, CurrentPin, secondWorkstation.Id, secondCredential.Id);

        var previous = _ctx.SessionRepository.Items.Single(s => s.WorkstationId == _workstation.Id);
        Assert.Equal(SessionEndReason.MovedToAnotherWorkstation, previous.EndReason);
        Assert.True(_ctx.SessionRepository.Items.Single(s => s.WorkstationId == secondWorkstation.Id).IsOpen);
    }

    // --- Completar el login -----------------------------------------------------------------

    [Fact]
    public async Task FirstLogin_NoticeThenPinChange_OpensTheSession()
    {
        var notice = _ctx.SeedNotice();
        var op = _ctx.SeedOperator(EmployeeNo, CurrentPin, mustChangePin: true);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        Assert.True(pending.RequiresPinChange);
        Assert.NotNull(pending.Notice);

        var result = await Complete(pending.Ticket, notice.Version, NewPin);

        var opened = Assert.IsType<LoginOutcome.Opened>(result.Value);
        Assert.Equal(op.Id, opened.OperatorId);
        Assert.False(op.MustChangePin);
        Assert.Single(_ctx.SessionRepository.Items);
        Assert.NotNull(Assert.Single(_ctx.LoginTickets.Items).ConsumedAt);

        var acceptance = Assert.Single(_ctx.Notices.Acceptances);
        Assert.Equal(op.Id, acceptance.OperatorId);
        Assert.Equal(notice.Version, acceptance.NoticeVersion);
        Assert.Equal(_workstation.Id, acceptance.WorkstationId);
        Assert.Equal(_ctx.Now, acceptance.AcceptedAt);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.OperatorPinChanged));

        Assert.IsType<LoginOutcome.Opened>((await Start(EmployeeNo, NewPin)).Value);
    }

    [Fact]
    public async Task Complete_FromAnotherDeviceCredential_IsRejected()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        var (otherWorkstation, otherCredential) = _ctx.SeedEnrolledWorkstation(number: 8);

        var result = await Complete(pending.Ticket, notice.Version, null, otherWorkstation.Id, otherCredential.Id);

        Assert.Equal(IdentityErrors.InvalidLoginTicket, result.Error);
        Assert.Empty(_ctx.SessionRepository.Items);
        Assert.Null(Assert.Single(_ctx.LoginTickets.Items).ConsumedAt);
        var evt = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.LoginTicketRejected));
        Assert.Equal(LoginTicketErrors.WrongDevice.Code, evt.Data!["reason"]);
        Assert.Equal(otherWorkstation.Id, evt.WorkstationId);
    }

    [Fact]
    public async Task Complete_WithAUsedTicket_IsRejected()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        await Complete(pending.Ticket, notice.Version, null);

        var second = await Complete(pending.Ticket, notice.Version, null);

        Assert.Equal(IdentityErrors.InvalidLoginTicket, second.Error);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.LoginTicketRejected));
    }

    [Fact]
    public async Task Complete_WithAnExpiredTicket_IsRejected()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        _ctx.Time.Advance(_ctx.Parameters.LoginTicketLifetime);

        Assert.Equal(IdentityErrors.InvalidLoginTicket, (await Complete(pending.Ticket, notice.Version, null)).Error);
    }

    [Fact]
    public async Task Complete_WithAnUnknownTicket_IsRejected()
    {
        _ctx.SeedNotice();

        Assert.Equal(IdentityErrors.InvalidLoginTicket, (await Complete("inventado", 1, null)).Error);
        Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.LoginTicketRejected));
    }

    [Fact]
    public async Task Complete_WithATrivialNewPin_FailsWithoutConsumingTheTicket()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin, mustChangePin: true);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        var savesBefore = _ctx.UnitOfWork.SaveCount;

        var trivial = await Complete(pending.Ticket, notice.Version, "123456");

        Assert.Equal(PinErrors.Trivial, trivial.Error);
        Assert.Equal(savesBefore, _ctx.UnitOfWork.SaveCount);
        Assert.Empty(_ctx.SessionRepository.Items);
        Assert.True((await Complete(pending.Ticket, notice.Version, NewPin)).IsSuccess);
    }

    [Fact]
    public async Task Complete_WithTheTemporaryPinAsNewPin_Fails()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin, mustChangePin: true);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);

        Assert.Equal(PinErrors.SameAsCurrent, (await Complete(pending.Ticket, notice.Version, CurrentPin)).Error);
    }

    [Fact]
    public async Task Complete_WithoutTheRequiredNewPin_Fails()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin, mustChangePin: true);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);

        Assert.Equal(IdentityErrors.NewPinRequired, (await Complete(pending.Ticket, notice.Version, null)).Error);
    }

    [Fact]
    public async Task Complete_WithoutAcceptingTheRequestedVersion_Fails()
    {
        var notice = _ctx.SeedNotice();
        _ctx.SeedOperator(EmployeeNo, CurrentPin);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);

        Assert.Equal(LoginTicketErrors.NoticeVersionMismatch, (await Complete(pending.Ticket, null, null)).Error);
        Assert.Empty(_ctx.SessionRepository.Items);
    }

    [Fact]
    public async Task Complete_AfterANewNoticeVersion_AsksAgainWithTheNewVersion()
    {
        var first = _ctx.SeedNotice("v1");
        _ctx.SeedOperator(EmployeeNo, CurrentPin);
        var pending = Assert.IsType<LoginOutcome.StepsRequired>((await Start(EmployeeNo, CurrentPin)).Value);
        var second = _ctx.SeedNotice("v2");

        var result = await Complete(pending.Ticket, first.Version, null);

        var again = Assert.IsType<LoginOutcome.StepsRequired>(result.Value);
        Assert.Equal(second.Version, again.Notice!.Version);
        Assert.Empty(_ctx.SessionRepository.Items);
        Assert.Empty(_ctx.Notices.Acceptances);
        Assert.IsType<LoginOutcome.Opened>((await Complete(again.Ticket, second.Version, null)).Value);
    }

    [Fact]
    public async Task Close_EndsTheSessionAsLogout()
    {
        SeedReadyOperator();
        var opened = Assert.IsType<LoginOutcome.Opened>((await Start(EmployeeNo, CurrentPin)).Value);
        var handler = new CloseOperatorSessionHandler(_ctx.Sessions, _ctx.UnitOfWork);

        var result = await handler.HandleAsync(new CloseOperatorSessionCommand(opened.SessionId), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionEndReason.Logout, _ctx.SessionRepository.Items.Single().EndReason);
    }

    [Fact]
    public void Commands_DoNotRevealSecretsInToString()
    {
        var start = new StartOperatorLoginCommand(Guid.NewGuid(), Guid.NewGuid(), EmployeeNo, CurrentPin);
        var complete = new CompleteOperatorLoginCommand(Guid.NewGuid(), Guid.NewGuid(), "ticket-secreto", 1, NewPin);

        Assert.DoesNotContain(CurrentPin, start.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(NewPin, complete.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ticket-secreto", complete.ToString(), StringComparison.Ordinal);
    }

    private Operator SeedReadyOperator(PrivacyNotice? notice = null)
    {
        notice ??= _ctx.SeedNotice();
        var op = _ctx.SeedOperator(EmployeeNo, CurrentPin);
        _ctx.SeedAcceptance(op, notice, _workstation.Id);
        return op;
    }

    private ManualLoginIdentityProvider IdentityProvider() =>
        new(_ctx.Operators, _ctx.PinHasher, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time, _ctx.ParametersOptions);

    private Task<Result<LoginOutcome>> Start(string employeeNumber, string pin, Guid? workstationId = null, Guid? credentialId = null)
    {
        var handler = new StartOperatorLoginHandler(
            IdentityProvider(), _ctx.Operators, _ctx.Notices, _ctx.LoginTickets, _ctx.Sessions, _ctx.Tokens, _ctx.UnitOfWork, _ctx.Time, _ctx.ParametersOptions);
        return handler.HandleAsync(
            new StartOperatorLoginCommand(workstationId ?? _workstation.Id, credentialId ?? _credential.Id, employeeNumber, pin),
            IdentityTestContext.Ct);
    }

    private Task<Result<LoginOutcome>> Complete(
        string ticket,
        int? acceptedVersion,
        string? newPin,
        Guid? workstationId = null,
        Guid? credentialId = null)
    {
        var handler = new CompleteOperatorLoginHandler(
            _ctx.Operators, _ctx.Notices, _ctx.LoginTickets, _ctx.Sessions, _ctx.Tokens, _ctx.PinHasher,
            _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time, _ctx.ParametersOptions);
        return handler.HandleAsync(
            new CompleteOperatorLoginCommand(workstationId ?? _workstation.Id, credentialId ?? _credential.Id, ticket, acceptedVersion, newPin),
            IdentityTestContext.Ct);
    }
}
