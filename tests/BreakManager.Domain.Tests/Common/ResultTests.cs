using BreakManager.Domain.Common;

namespace BreakManager.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly Error SampleError = new("Sample.Code", "Mensaje de prueba");

    [Fact]
    public void Success_IsSuccess_WithNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_IsFailure_WithError()
    {
        var result = Result.Failure(SampleError);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
    }

    [Fact]
    public void Failure_WithErrorNone_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailure_AccessingValue_Throws()
    {
        var result = Result.Failure<int>(SampleError);

        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void GenericFailure_WithErrorNone_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccess()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        Result<string> result = SampleError;

        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
    }

    [Fact]
    public void Error_WithSameCodeAndMessage_AreEqual()
    {
        Assert.Equal(new Error("X", "y"), new Error("X", "y"));
    }
}
