using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class LabelCropTests
{
    [Fact]
    public void Default_Is_A_Centred_Crop_Of_The_Largest_Square()
    {
        LabelCrop.Default.FocusX.Should().Be(0.5);
        LabelCrop.Default.FocusY.Should().Be(0.5);
        LabelCrop.Default.Zoom.Should().Be(1.0);
    }

    [Theory]
    [InlineData(0.0, 0.0, 1.0)]
    [InlineData(1.0, 1.0, 4.0)]
    [InlineData(0.25, 0.8, 2.5)]
    public void Create_Accepts_Values_At_And_Within_The_Bounds(double fx, double fy, double zoom)
    {
        var crop = LabelCrop.Create(fx, fy, zoom);

        crop.FocusX.Should().Be(fx);
        crop.FocusY.Should().Be(fy);
        crop.Zoom.Should().Be(zoom);
    }

    [Theory]
    [InlineData(-0.01, 0.5, 1.0)]
    [InlineData(1.01, 0.5, 1.0)]
    [InlineData(0.5, -0.01, 1.0)]
    [InlineData(0.5, 1.01, 1.0)]
    [InlineData(0.5, 0.5, 0.99)]
    [InlineData(0.5, 0.5, 4.01)]
    [InlineData(double.NaN, 0.5, 1.0)]
    [InlineData(0.5, double.NaN, 1.0)]
    [InlineData(0.5, 0.5, double.NaN)]
    [InlineData(0.5, 0.5, double.PositiveInfinity)]
    public void Create_Rejects_Out_Of_Range_Or_Non_Finite_Values(double fx, double fy, double zoom)
    {
        var act = () => LabelCrop.Create(fx, fy, zoom);

        act.Should().Throw<InvalidLabelCropException>();
    }

    [Fact]
    public void Equal_Values_Are_Equal()
    {
        LabelCrop.Create(0.5, 0.5, 1.0).Should().Be(LabelCrop.Default);
    }
}
