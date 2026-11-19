using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class OperatorTests
{
    private const string TemporaryPin = "482913";
    private const string WrongPin = "000001";

    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly LockoutPolicy Policy = new(
        attempts: 5,
        duration: TimeSpan.FromMinutes(5),
        lockoutsBeforeManualUnlock: 3,
        window: TimeSpan.FromHours(24));

    private readonly FakePinHasher _hasher = new();

    [Fact]
    public void Create_AssignsATemporaryPin_AndRequiresChangingIt()
    {
        var op = NewOperator();

        Assert.True(op.Active);
        Assert.True(op.MustChangePin);
        Assert.Equal(LoginAttemptResult.Succeeded, op.AttemptLogin(TemporaryPin, T0, Policy, _hasher));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutFullName_Fails(string fullName)
    {
        var result = Operator.Create(
            Guid.NewGuid(), Employee(), fullName, Group.C, null, _hasher.Hash(NewPin(TemporaryPin)));

        Assert.Equal(OperatorErrors.FullNameRequired, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Create_WithHomeWorkstationOutOfRange_Fails(int number)
    {
        var result = Operator.Create(
            Guid.NewGuid(), Employee(), "Operador", Group.C, number, _hasher.Hash(NewPin(TemporaryPin)));

        Assert.Equal(OperatorErrors.HomeWorkstationOutOfRange, result.Error);
    }

    [Fact]
    public void AttemptLogin_WithWrongPin_IsInvalidCredentials()
    {
        var op = NewOperator();

        Assert.Equal(LoginAttemptResult.InvalidCredentials, op.AttemptLogin(WrongPin, T0, Policy, _hasher));
        Assert.Equal(1, op.FailedAttempts);
    }

    [Fact]
    public void AttemptLogin_WhenInactive_IsInvalidCredentials_EvenWithTheRightPin()
    {
        var op = NewOperator();
        op.Deactivate();

        Assert.Equal(LoginAttemptResult.InvalidCredentials, op.AttemptLogin(TemporaryPin, T0, Policy, _hasher));
    }

    [Fact]
    public void AttemptLogin_FifthFailure_LocksTheOperatorForTheConfiguredDuration()
    {
        var op = NewOperator();

        FailTimes(op, 4, T0);
        var fifth = op.AttemptLogin(WrongPin, T0, Policy, _hasher);

        Assert.Equal(LoginAttemptResult.LockedOut, fifth);
        Assert.Equal(T0 + Policy.Duration, op.LockedUntil);
        Assert.Equal(LoginAttemptResult.LockedOut, op.AttemptLogin(TemporaryPin, T0.AddMinutes(4), Policy, _hasher));
        Assert.Equal(LoginAttemptResult.Succeeded, op.AttemptLogin(TemporaryPin, T0.AddMinutes(5), Policy, _hasher));
    }

    [Fact]
    public void AttemptLogin_WhileLockedOut_DoesNotCountAttempts()
    {
        var op = NewOperator();
        FailTimes(op, 5, T0);

        FailTimes(op, 10, T0.AddMinutes(1));

        Assert.Equal(0, op.FailedAttempts);
        Assert.False(op.RequiresManualUnlock);
    }

    [Fact]
    public void AttemptLogin_SuccessResetsTheFailedAttempts()
    {
        var op = NewOperator();
        FailTimes(op, 4, T0);

        op.AttemptLogin(TemporaryPin, T0, Policy, _hasher);

        Assert.Equal(0, op.FailedAttempts);
        Assert.Equal(LoginAttemptResult.InvalidCredentials, op.AttemptLogin(WrongPin, T0, Policy, _hasher));
    }

    [Fact]
    public void AttemptLogin_ThirdLockoutWithinTheWindow_RequiresManualUnlock()
    {
        var op = NewOperator();

        FailTimes(op, 5, T0);
        FailTimes(op, 5, T0.AddMinutes(10));
        FailTimes(op, 4, T0.AddMinutes(20));
        var third = op.AttemptLogin(WrongPin, T0.AddMinutes(20), Policy, _hasher);

        Assert.Equal(LoginAttemptResult.LockedOutUntilManualUnlock, third);
        Assert.True(op.RequiresManualUnlock);
        Assert.Equal(LoginAttemptResult.LockedOut, op.AttemptLogin(TemporaryPin, T0.AddHours(3), Policy, _hasher));
    }

    [Fact]
    public void AttemptLogin_LockoutsOutsideTheWindow_DoNotCount()
    {
        var op = NewOperator();

        FailTimes(op, 5, T0);
        FailTimes(op, 5, T0.AddMinutes(10));
        var third = FailTimes(op, 5, T0.AddHours(25));

        Assert.Equal(LoginAttemptResult.LockedOut, third);
        Assert.False(op.RequiresManualUnlock);
    }

    [Fact]
    public void AttemptLogin_UsesThePolicyValues_NotConstants()
    {
        var op = NewOperator();
        var strict = new LockoutPolicy(2, TimeSpan.FromMinutes(1), 1, TimeSpan.FromHours(1));

        op.AttemptLogin(WrongPin, T0, strict, _hasher);
        var second = op.AttemptLogin(WrongPin, T0, strict, _hasher);

        Assert.Equal(LoginAttemptResult.LockedOutUntilManualUnlock, second);
    }

    [Fact]
    public void Unlock_ClearsTheLockout_AndTheLockoutHistory()
    {
        var op = NewOperator();
        FailTimes(op, 5, T0);
        FailTimes(op, 5, T0.AddMinutes(10));
        FailTimes(op, 5, T0.AddMinutes(20));

        op.Unlock();

        Assert.False(op.RequiresManualUnlock);
        Assert.Null(op.LockedUntil);
        Assert.Equal(0, op.FailedAttempts);
        Assert.Empty(op.RecentLockouts);
        Assert.Equal(LoginAttemptResult.Succeeded, op.AttemptLogin(TemporaryPin, T0.AddMinutes(21), Policy, _hasher));
        Assert.Equal(LoginAttemptResult.LockedOut, FailTimes(op, 5, T0.AddMinutes(22)));
    }

    [Fact]
    public void ChangePin_ReplacesThePin_AndClearsMustChangePin()
    {
        var op = NewOperator();

        var result = op.ChangePin(NewPin("736251"), _hasher);

        Assert.True(result.IsSuccess);
        Assert.False(op.MustChangePin);
        Assert.Equal(LoginAttemptResult.Succeeded, op.AttemptLogin("736251", T0, Policy, _hasher));
        Assert.Equal(LoginAttemptResult.InvalidCredentials, op.AttemptLogin(TemporaryPin, T0, Policy, _hasher));
    }

    [Fact]
    public void ChangePin_ToTheCurrentPin_Fails()
    {
        var op = NewOperator();

        var result = op.ChangePin(NewPin(TemporaryPin), _hasher);

        Assert.Equal(PinErrors.SameAsCurrent, result.Error);
        Assert.True(op.MustChangePin);
    }

    [Fact]
    public void AssignTemporaryPin_RequiresChangingItAgain()
    {
        var op = NewOperator();
        op.ChangePin(NewPin("736251"), _hasher);

        op.AssignTemporaryPin(_hasher.Hash(NewPin("918273")));

        Assert.True(op.MustChangePin);
        Assert.Equal(LoginAttemptResult.Succeeded, op.AttemptLogin("918273", T0, Policy, _hasher));
    }

    [Fact]
    public void Update_ChangesNameGroupAndHomeWorkstation()
    {
        var op = NewOperator();

        var result = op.Update("  Otro Nombre ", Group.A, 7);

        Assert.True(result.IsSuccess);
        Assert.Equal("Otro Nombre", op.FullName);
        Assert.Equal(Group.A, op.Group);
        Assert.Equal(7, op.HomeWorkstationNumber);
    }

    [Fact]
    public void LockoutPolicy_RejectsNonPositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(0, TimeSpan.FromMinutes(5), 3, TimeSpan.FromHours(24)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(5, TimeSpan.Zero, 3, TimeSpan.FromHours(24)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(5, TimeSpan.FromMinutes(5), 0, TimeSpan.FromHours(24)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(5, TimeSpan.FromMinutes(5), 3, TimeSpan.Zero));
    }

    private static EmployeeNumber Employee() => EmployeeNumber.Create("20481").Value;

    private static Pin NewPin(string value) => Pin.Create(value, Employee()).Value;

    private Operator NewOperator() =>
        Operator.Create(
            Guid.NewGuid(),
            Employee(),
            "Operador de prueba",
            Group.C,
            homeWorkstationNumber: 3,
            _hasher.Hash(NewPin(TemporaryPin))).Value;

    private LoginAttemptResult FailTimes(Operator op, int times, DateTimeOffset at)
    {
        var last = LoginAttemptResult.InvalidCredentials;
        for (var i = 0; i < times; i++)
        {
            last = op.AttemptLogin(WrongPin, at, Policy, _hasher);
        }

        return last;
    }
}
