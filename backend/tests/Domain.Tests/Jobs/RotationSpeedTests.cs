using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class RotationSpeedTests
{
    [Fact]
    public void Create_Accepts_A_Value_Within_The_Allowed_Range()
    {
        var speed = RotationSpeed.Create(27);

        speed.SecondsPerRotation.Should().Be(27);
    }

    [Theory]
    [InlineData(11.99)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(90.01)]
    [InlineData(100)]
    public void Create_Rejects_Values_Outside_The_Allowed_Range(double secondsPerRotation)
    {
        var act = () => RotationSpeed.Create(secondsPerRotation);

        act.Should().Throw<InvalidRotationSpeedException>();
    }

    [Fact]
    public void Create_Accepts_The_Boundary_Values()
    {
        RotationSpeed.Create(RotationSpeed.MinSecondsPerRotation).SecondsPerRotation.Should().Be(RotationSpeed.MinSecondsPerRotation);
        RotationSpeed.Create(RotationSpeed.MaxSecondsPerRotation).SecondsPerRotation.Should().Be(RotationSpeed.MaxSecondsPerRotation);
    }

    [Fact]
    public void Default_Is_The_Homepage_Records_Eighteen_Second_Spin()
    {
        RotationSpeed.Default.SecondsPerRotation.Should().Be(18.0);
    }

    [Fact]
    public void The_Maximum_Allows_A_Dramatically_Slow_Spin()
    {
        RotationSpeed.MaxSecondsPerRotation.Should().Be(90.0);
    }

    [Fact]
    public void The_Minimum_Is_Not_Faster_Than_Twelve_Seconds_Per_Spin()
    {
        RotationSpeed.MinSecondsPerRotation.Should().Be(12.0);
    }
}
