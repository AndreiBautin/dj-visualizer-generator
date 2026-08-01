using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class VideoPresetTests
{
    [Fact]
    public void FullHd1080p_Is_1920_By_1080()
    {
        VideoPreset.FullHd1080p.Width.Should().Be(1920);
        VideoPreset.FullHd1080p.Height.Should().Be(1080);
    }

    [Fact]
    public void Hd720p_Is_1280_By_720()
    {
        VideoPreset.Hd720p.Width.Should().Be(1280);
        VideoPreset.Hd720p.Height.Should().Be(720);
    }

    [Theory]
    [InlineData("1080p")]
    [InlineData("720p")]
    public void FromName_Resolves_Known_Presets(string name)
    {
        var act = () => VideoPreset.FromName(name);

        act.Should().NotThrow();
    }

    [Fact]
    public void FromName_Rejects_Unknown_Preset()
    {
        var act = () => VideoPreset.FromName("4k");

        act.Should().Throw<InvalidVideoPresetException>();
    }
}
