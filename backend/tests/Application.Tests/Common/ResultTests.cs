using DjVisualizer.Application.Common;
using FluentAssertions;

namespace DjVisualizer.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_Produces_A_Successful_Result_With_No_Error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_Produces_A_Failed_Result_With_The_Given_Error()
    {
        var error = Error.Validation("bad input");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_Success_Carries_The_Value()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Generic_Failure_Carries_The_Error_And_No_Value()
    {
        var error = Error.NotFound("job not found");

        var result = Result<int>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be(error);
    }
}
