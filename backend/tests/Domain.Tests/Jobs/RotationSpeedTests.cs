using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class RotationSpeedTests
{
    [Fact]
    public void Create_Accepts_A_Value_Within_The_Allowed_Range()
    {
        var speed = RotationSpeed.Create(4.5);

        speed.SecondsPerRotation.Should().Be(4.5);
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(15.01)]
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
    public void Default_Is_Slower_Than_A_Two_Second_Spin()
    {
        RotationSpeed.Default.SecondsPerRotation.Should().BeGreaterThan(2.0);
    }

    [Fact]
    public void The_Maximum_Allows_A_Dramatically_Slow_Spin()
    {
        RotationSpeed.MaxSecondsPerRotation.Should().Be(15.0);
    }
}
