using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class EmployeeNumberTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("  20481 ")]
    public void Create_WithDigits_Succeeds_AndTrims(string input)
    {
        var result = EmployeeNumber.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input.Trim(), result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12a4")]
    [InlineData("12-34")]
    [InlineData("123456789012345678901")]
    public void Create_WithInvalidInput_Fails(string input)
    {
        var result = EmployeeNumber.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal(EmployeeNumberErrors.Invalid, result.Error);
    }

    [Fact]
    public void EmployeeNumbers_WithSameValue_AreEqual()
    {
        Assert.Equal(EmployeeNumber.Create("1234").Value, EmployeeNumber.Create(" 1234").Value);
    }
}
