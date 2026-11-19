using BreakManager.Application.Identity;

namespace BreakManager.Application.Tests.Identity;

public sealed class IdentityParametersTests
{
    private readonly IdentityParametersValidator _validator = new();

    [Fact]
    public void Defaults_AreTheInitialValuesOfTheSpec_AndValid()
    {
        var parameters = new IdentityParameters();

        Assert.Equal(5, parameters.OperatorLockoutAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), parameters.OperatorLockoutDuration);
        Assert.Equal(3, parameters.OperatorLockoutsBeforeManualUnlock);
        Assert.Equal(TimeSpan.FromHours(24), parameters.OperatorLockoutWindow);
        Assert.Equal(TimeSpan.FromHours(24), parameters.EnrollmentCodeLifetime);
        Assert.Equal(TimeSpan.FromHours(8), parameters.AgentSessionLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), parameters.LoginTicketLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), parameters.WebSessionRevalidationInterval);
        Assert.Equal(12, parameters.WebPasswordMinLength);
        Assert.True(_validator.Validate(null, parameters).Succeeded);
    }

    [Fact]
    public void Validate_RejectsNonPositiveValues_NamingEachOne()
    {
        var parameters = new IdentityParameters
        {
            OperatorLockoutAttempts = 0,
            LoginTicketLifetime = TimeSpan.Zero,
            WebPasswordMinLength = -1,
        };

        var result = _validator.Validate(null, parameters);

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
        Assert.Contains(result.Failures!, f => f.Contains("Identity:OperatorLockoutAttempts", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("Identity:LoginTicketLifetime", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("Identity:WebPasswordMinLength", StringComparison.Ordinal));
    }

    [Fact]
    public void ToOperatorLockoutPolicy_UsesTheConfiguredValues()
    {
        var parameters = new IdentityParameters
        {
            OperatorLockoutAttempts = 7,
            OperatorLockoutDuration = TimeSpan.FromMinutes(2),
            OperatorLockoutsBeforeManualUnlock = 4,
            OperatorLockoutWindow = TimeSpan.FromHours(12),
        };

        var policy = parameters.ToOperatorLockoutPolicy();

        Assert.Equal(7, policy.Attempts);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.Duration);
        Assert.Equal(4, policy.LockoutsBeforeManualUnlock);
        Assert.Equal(TimeSpan.FromHours(12), policy.Window);
    }
}
