using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class PinTests
{
    private static readonly EmployeeNumber Employee = EmployeeNumber.Create("20481").Value;

    [Theory]
    [InlineData("482913")]
    [InlineData("102938")]
    [InlineData("123457")]
    public void Create_WithSixNonTrivialDigits_Succeeds(string value)
    {
        var result = Pin.Create(value, Employee);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 482913")]
    public void Create_WithoutExactlySixDigits_FailsWithInvalidFormat(string value)
    {
        var result = Pin.Create(value, Employee);

        Assert.True(result.IsFailure);
        Assert.Equal(PinErrors.InvalidFormat, result.Error);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("777777")]
    [InlineData("012345")]
    [InlineData("123456")]
    [InlineData("456789")]
    [InlineData("987654")]
    [InlineData("543210")]
    public void Create_WithRepeatedOrSequentialDigits_FailsAsTrivial(string value)
    {
        var result = Pin.Create(value, Employee);

        Assert.True(result.IsFailure);
        Assert.Equal(PinErrors.Trivial, result.Error);
    }

    [Fact]
    public void Create_EqualToTheEmployeeNumber_FailsAsTrivial()
    {
        var employee = EmployeeNumber.Create("482913").Value;

        var result = Pin.Create("482913", employee);

        Assert.Equal(PinErrors.Trivial, result.Error);
    }

    [Fact]
    public void Create_EqualToTheLastSixDigitsOfALongerEmployeeNumber_FailsAsTrivial()
    {
        var employee = EmployeeNumber.Create("1000482913").Value;

        var result = Pin.Create("482913", employee);

        Assert.Equal(PinErrors.Trivial, result.Error);
    }

    [Fact]
    public void ToString_DoesNotRevealThePin()
    {
        var pin = Pin.Create("482913", Employee).Value;

        Assert.DoesNotContain("482913", pin.ToString(), StringComparison.Ordinal);
    }
}
