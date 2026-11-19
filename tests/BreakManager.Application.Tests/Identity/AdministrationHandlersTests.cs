using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Administration;
using BreakManager.Application.Tests.Fakes;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Tests.Identity;

public sealed class OperatorAdministrationTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly IdentityTestContext _ctx = new();

    [Fact]
    public async Task CreateOperator_WithATemporaryPin_RequiresChangingIt()
    {
        var result = await Create("20481", "482913");

        Assert.True(result.IsSuccess);
        var op = Assert.Single(_ctx.Operators.Items);
        Assert.Equal(result.Value, op.Id);
        Assert.True(op.MustChangePin);
        Assert.Equal(_ctx.PinHasher.Hash(Pin.Create("482913", op.EmployeeNumber).Value), op.PinHash);
    }

    [Fact]
    public async Task CreateOperator_WithADuplicateEmployeeNumber_Fails()
    {
        _ctx.SeedOperator("20481");

        Assert.Equal(IdentityErrors.EmployeeNumberTaken, (await Create("20481", "482913")).Error);
    }

    [Theory]
    [InlineData("20481", "123456")]
    [InlineData("20481", "12345")]
    [InlineData("x", "482913")]
    public async Task CreateOperator_WithInvalidData_Fails(string employeeNumber, string pin)
    {
        Assert.True((await Create(employeeNumber, pin)).IsFailure);
        Assert.Empty(_ctx.Operators.Items);
    }

    [Fact]
    public async Task UpdateOperator_ChangesTheData()
    {
        var op = _ctx.SeedOperator();
        var handler = new UpdateOperatorHandler(_ctx.Operators, _ctx.UnitOfWork);

        var result = await handler.HandleAsync(new UpdateOperatorCommand(op.Id, "Nuevo Nombre", Group.A, 5), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Nuevo Nombre", op.FullName);
        Assert.Equal(Group.A, op.Group);
    }

    [Fact]
    public async Task SetOperatorActive_DeactivatesAndReactivates()
    {
        var op = _ctx.SeedOperator();
        var handler = new SetOperatorActiveHandler(_ctx.Operators, _ctx.UnitOfWork);

        await handler.HandleAsync(new SetOperatorActiveCommand(op.Id, false), IdentityTestContext.Ct);
        Assert.False(op.Active);

        await handler.HandleAsync(new SetOperatorActiveCommand(op.Id, true), IdentityTestContext.Ct);
        Assert.True(op.Active);
    }

    [Fact]
    public async Task AssignTemporaryPin_ReplacesThePin_AndRecordsOperatorPinReset()
    {
        var op = _ctx.SeedOperator(pin: "482913");
        var handler = new AssignTemporaryPinHandler(_ctx.Operators, _ctx.PinHasher, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new AssignTemporaryPinCommand(op.Id, "918273", Admin), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.True(op.MustChangePin);
        Assert.True(_ctx.PinHasher.Verify(op.PinHash, "918273"));
        Assert.Equal(Admin, Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.OperatorPinReset)).ActorId);
    }

    [Fact]
    public async Task AssignTemporaryPin_Trivial_Fails()
    {
        var op = _ctx.SeedOperator();
        var handler = new AssignTemporaryPinHandler(_ctx.Operators, _ctx.PinHasher, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new AssignTemporaryPinCommand(op.Id, "111111", Admin), IdentityTestContext.Ct);

        Assert.Equal(PinErrors.Trivial, result.Error);
    }

    [Fact]
    public async Task UnlockOperator_ClearsTheLockout_AndRecordsOperatorUnlocked()
    {
        var op = _ctx.SeedOperator();
        var strict = new LockoutPolicy(1, TimeSpan.FromMinutes(5), 1, TimeSpan.FromHours(24));
        op.AttemptLogin("000001", _ctx.Now, strict, _ctx.PinHasher);
        Assert.True(op.RequiresManualUnlock);
        var handler = new UnlockOperatorHandler(_ctx.Operators, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        var result = await handler.HandleAsync(new UnlockOperatorCommand(op.Id, Admin), IdentityTestContext.Ct);

        Assert.True(result.IsSuccess);
        Assert.False(op.IsLockedOut(_ctx.Now));
        var evt = Assert.Single(_ctx.SecurityEvents.OfType(SecurityEventType.OperatorUnlocked));
        Assert.Equal(Admin, evt.ActorId);
        Assert.Equal(op.Id.ToString(), evt.Data!["operatorId"]);
    }

    [Fact]
    public async Task Handlers_ForAMissingOperator_FailWithNotFound()
    {
        var unlock = new UnlockOperatorHandler(_ctx.Operators, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        Assert.Equal(
            IdentityErrors.OperatorNotFound,
            (await unlock.HandleAsync(new UnlockOperatorCommand(Guid.NewGuid(), Admin), IdentityTestContext.Ct)).Error);
    }

    [Fact]
    public void Commands_DoNotRevealPins()
    {
        Assert.DoesNotContain("482913", new CreateOperatorCommand("20481", "Nombre", Group.C, null, "482913").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("482913", new AssignTemporaryPinCommand(Guid.NewGuid(), "482913", Admin).ToString(), StringComparison.Ordinal);
    }

    private Task<Result<Guid>> Create(string employeeNumber, string pin)
    {
        var handler = new CreateOperatorHandler(_ctx.Operators, _ctx.PinHasher, _ctx.UnitOfWork, _ctx.Time);
        return handler.HandleAsync(new CreateOperatorCommand(employeeNumber, "Operador", Group.C, 3, pin), IdentityTestContext.Ct);
    }
}

public sealed class UserAdministrationTests
{
    private readonly FakeUserAccountService _users = new();
    private readonly Guid _admin;

    public UserAdministrationTests()
    {
        _admin = _users.Seed("admin", WebRole.Administrator, null);
    }

    [Fact]
    public async Task CreateUser_Supervisor_RequiresAGroup()
    {
        var handler = new CreateUserHandler(_users);

        var without = await handler.HandleAsync(new CreateUserCommand("sup", WebRole.Supervisor, null, "contraseña-larga-1"), IdentityTestContext.Ct);
        var with = await handler.HandleAsync(new CreateUserCommand("sup", WebRole.Supervisor, Group.C, "contraseña-larga-1"), IdentityTestContext.Ct);

        Assert.Equal(IdentityErrors.SupervisorGroupRequired, without.Error);
        Assert.True(with.IsSuccess);
    }

    [Fact]
    public async Task CreateUser_OtherRoles_CannotHaveAGroup()
    {
        var handler = new CreateUserHandler(_users);

        var result = await handler.HandleAsync(new CreateUserCommand("coord", WebRole.Coordinator, Group.A, "contraseña-larga-1"), IdentityTestContext.Ct);

        Assert.Equal(IdentityErrors.SupervisorGroupRequired, result.Error);
    }

    [Fact]
    public async Task UpdateUserRole_ChecksTheGroupRule()
    {
        var user = _users.Seed("calidad", WebRole.Quality, null);
        var handler = new UpdateUserRoleHandler(_users);

        Assert.Equal(
            IdentityErrors.SupervisorGroupRequired,
            (await handler.HandleAsync(new UpdateUserRoleCommand(user, WebRole.Supervisor, null), IdentityTestContext.Ct)).Error);
        Assert.True((await handler.HandleAsync(new UpdateUserRoleCommand(user, WebRole.Supervisor, Group.B), IdentityTestContext.Ct)).IsSuccess);
        Assert.Equal(WebRole.Supervisor, _users.Accounts[user].Role);
    }

    [Fact]
    public async Task SetUserActive_CannotDeactivateYourself()
    {
        var handler = new SetUserActiveHandler(_users);

        var result = await handler.HandleAsync(new SetUserActiveCommand(_admin, false, _admin), IdentityTestContext.Ct);

        Assert.Equal(IdentityErrors.CannotDeactivateSelf, result.Error);
        Assert.True(_users.Accounts[_admin].Active);
    }

    [Fact]
    public async Task SetUserActive_DeactivatesAnotherUser()
    {
        var other = _users.Seed("otro", WebRole.Quality, null);
        var handler = new SetUserActiveHandler(_users);

        Assert.True((await handler.HandleAsync(new SetUserActiveCommand(other, false, _admin), IdentityTestContext.Ct)).IsSuccess);
        Assert.False(_users.Accounts[other].Active);
    }

    [Fact]
    public async Task ResetUserPassword_DelegatesToTheAccountService()
    {
        var other = _users.Seed("otro", WebRole.Quality, null);
        var handler = new ResetUserPasswordHandler(_users);

        Assert.True((await handler.HandleAsync(new ResetUserPasswordCommand(other, "temporal-larga-1"), IdentityTestContext.Ct)).IsSuccess);
        Assert.Equal("temporal-larga-1", _users.Passwords[other]);
    }

    [Fact]
    public void Commands_DoNotRevealPasswords()
    {
        Assert.DoesNotContain("secreta", new CreateUserCommand("u", WebRole.Quality, null, "secreta").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secreta", new ResetUserPasswordCommand(Guid.NewGuid(), "secreta").ToString(), StringComparison.Ordinal);
    }
}

public sealed class PrivacyNoticeAdministrationTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly IdentityTestContext _ctx = new();

    [Fact]
    public async Task Publish_CreatesConsecutiveVersions_AndRecordsTheEvent()
    {
        var handler = new PublishPrivacyNoticeHandler(_ctx.Notices, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        var first = await handler.HandleAsync(new PublishPrivacyNoticeCommand("Aviso v1", Admin), IdentityTestContext.Ct);
        var second = await handler.HandleAsync(new PublishPrivacyNoticeCommand("Aviso v2", Admin), IdentityTestContext.Ct);

        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
        Assert.Equal("Aviso v2", (await _ctx.Notices.GetCurrentAsync(IdentityTestContext.Ct))!.Text);
        Assert.Equal(2, _ctx.SecurityEvents.OfType(SecurityEventType.PrivacyNoticePublished).Count());
    }

    [Fact]
    public async Task Publish_WithoutText_Fails()
    {
        var handler = new PublishPrivacyNoticeHandler(_ctx.Notices, _ctx.SecurityEvents, _ctx.UnitOfWork, _ctx.Time);

        Assert.Equal(
            PrivacyNoticeErrors.TextRequired,
            (await handler.HandleAsync(new PublishPrivacyNoticeCommand("  ", Admin), IdentityTestContext.Ct)).Error);
    }
}
