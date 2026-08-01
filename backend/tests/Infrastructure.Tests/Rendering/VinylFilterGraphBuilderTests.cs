using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Rendering;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

public class VinylFilterGraphBuilderTests
{
    private const string FontFilePath = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    [Fact]
    public void BuildStaticVinylGraph_Sizes_The_Circular_Artwork_And_Border_For_1080p()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.FullHd1080p);

        graph.Should().Contain("[0:v]scale=799:799:force_original_aspect_ratio=increase,crop=799:799");
        graph.Should().Contain("s=815x815"); // ring: diameter (799) + 2 * border (8)
        graph.Should().EndWith("[vinyl_static]");
    }

    [Fact]
    public void BuildStaticVinylGraph_Sizes_The_Circular_Artwork_And_Border_For_720p()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.Hd720p);

        graph.Should().Contain("[0:v]scale=533:533:force_original_aspect_ratio=increase,crop=533:533");
        graph.Should().Contain("s=543x543"); // ring: diameter (533) + 2 * border (5)
        graph.Should().EndWith("[vinyl_static]");
    }

    [Fact]
    public void BuildStaticVinylGraph_Contains_No_Per_Frame_Filters()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.FullHd1080p);

        // The whole point of the two-pass split: geq/mask work happens once here, not per output frame.
        graph.Should().NotContain("rotate=");
        graph.Should().NotContain("drawtext=");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Rotates_At_One_Full_Turn_Per_The_Given_Period()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        // 2*pi radians / 2 seconds = pi radians/second.
        graph.Should().Contain("rotate=3.14159");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Uses_A_Slower_Angular_Velocity_For_A_Longer_Period()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 4.0);

        // 2*pi radians / 4 seconds = pi/2 rad/second.
        graph.Should().Contain("rotate=1.5707963267948966");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Sizes_The_Background_And_Text_For_1080p()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain("s=1920x1080"); // full-frame black background
        graph.Should().Contain("ow=815:oh=815"); // rotate canvas matches the ring diameter
        graph.Should().Contain("fontsize=48");
        graph.Should().Contain("y=h-97");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Sizes_The_Background_And_Text_For_720p()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.Hd720p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain("s=1280x720");
        graph.Should().Contain("ow=543:oh=543");
        graph.Should().Contain("fontsize=32");
        graph.Should().Contain("y=h-65");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Starts_From_Input_Zero_And_Ends_With_The_Final_Pad()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().StartWith("[0:v]rotate=");
        graph.Should().EndWith("[final]");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Escapes_Colons_Quotes_And_Percent_Signs_In_The_Title()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(
            VideoPreset.FullHd1080p, "Deep:House B's Anthem 100%", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain(@"text='Deep\:House B\'s Anthem 100\%'");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Escapes_Backslashes_Before_Other_Characters()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(
            VideoPreset.FullHd1080p, @"back\slash", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain(@"text='back\\slash'");
    }
}
