using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class EnrollmentCodeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    [Fact]
    public void Issue_ExpiresAfterTheLifetime()
    {
        var code = NewCode();

        Assert.Equal(T0 + Lifetime, code.ExpiresAt);
        Assert.True(code.IsUsable(T0));
    }

    [Fact]
    public void Redeem_WithinTheLifetime_Succeeds_Once()
    {
        var code = NewCode();

        var first = code.Redeem(T0.AddHours(1));
        var second = code.Redeem(T0.AddHours(2));

        Assert.True(first.IsSuccess);
        Assert.Equal(T0.AddHours(1), code.RedeemedAt);
        Assert.Equal(EnrollmentCodeErrors.AlreadyUsed, second.Error);
    }

    [Fact]
    public void Redeem_AtOrAfterExpiration_Fails()
    {
        var code = NewCode();

        var result = code.Redeem(T0 + Lifetime);

        Assert.Equal(EnrollmentCodeErrors.Expired, result.Error);
        Assert.Null(code.RedeemedAt);
    }

    [Fact]
    public void Redeem_AfterBeingInvalidated_Fails()
    {
        var code = NewCode();
        code.Invalidate(T0.AddMinutes(1));

        var result = code.Redeem(T0.AddMinutes(2));

        Assert.Equal(EnrollmentCodeErrors.Invalidated, result.Error);
        Assert.False(code.IsUsable(T0.AddMinutes(2)));
    }

    [Fact]
    public void Invalidate_DoesNotAffectARedeemedCode()
    {
        var code = NewCode();
        code.Redeem(T0.AddMinutes(1));

        code.Invalidate(T0.AddMinutes(2));

        Assert.Null(code.InvalidatedAt);
    }

    [Fact]
    public void Issue_RejectsANonPositiveLifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnrollmentCode.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", T0, TimeSpan.Zero));
    }

    private static EnrollmentCode NewCode() => EnrollmentCode.Issue(Guid.NewGuid(), Guid.NewGuid(), "code-hash", T0, Lifetime);
}
