using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Rendering;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

public class VinylFilterGraphBuilderTests
{
    private const string FontFilePath = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    /// <summary>
    /// The record is the homepage's VinylRecord: label at 40% of the disc, spindle hole at 6%.
    /// 1080p: disc 0.74 * 1080 = 799, label 320, hole 48 inside a ring two preview pixels wide.
    /// </summary>
    [Fact]
    public void BuildStaticVinylGraph_Draws_The_Homepage_Record_At_1080p()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.FullHd1080p);

        graph.Should().Contain("color=c=black:s=799x799");
        graph.Should().Contain("[0:v]scale=320:320:force_original_aspect_ratio=increase,crop=320:320");
        graph.Should().Contain("s=58x58"); // hole 48 + 2 * ring 5
        graph.Should().EndWith("[vinyl_static]");
    }

    [Fact]
    public void BuildStaticVinylGraph_Draws_The_Homepage_Record_At_720p()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.Hd720p);

        graph.Should().Contain("color=c=black:s=533x533");
        graph.Should().Contain("[0:v]scale=213:213:force_original_aspect_ratio=increase,crop=213:213");
        graph.Should().EndWith("[vinyl_static]");
    }

    [Fact]
    public void BuildStaticVinylGraph_Cuts_Grooves_Into_The_Disc_And_Labels_It_With_The_Artwork()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.FullHd1080p);

        // The grooves repeat on a radius period; the label goes over the disc, the hole over both.
        graph.Should().Contain("mod(hypot(");
        graph.Should().MatchRegex(@"\[disc\]\[label\]overlay=.*\[with_label\]\[hole\]overlay=");
    }

    [Fact]
    public void BuildStaticVinylGraph_Contains_No_Per_Frame_Filters()
    {
        var graph = VinylFilterGraphBuilder.BuildStaticVinylGraph(VideoPreset.FullHd1080p);

        // The whole point of the pass split: geq work happens once here, not per output frame.
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
    public void BuildRotatingCompositeGraph_Sizes_The_Rotation_Canvas_And_Text_For_1080p()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain("ow=799:oh=799"); // rotate canvas matches the record
        graph.Should().Contain("fontsize=48");
        graph.Should().Contain("y=h-97");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Sizes_The_Rotation_Canvas_And_Text_For_720p()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.Hd720p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain("ow=533:oh=533");
        graph.Should().Contain("fontsize=32");
        graph.Should().Contain("y=h-65");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Composites_Onto_The_Second_Input_As_The_Background()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.Hd720p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        // Input 1 is the pre-rendered backdrop (see BuildBackgroundGraph) - no live generator
        // here, and no per-frame background computation.
        graph.Should().Contain("[1:v][vinyl_rotating]overlay=");
        graph.Should().NotContain("color=c=black");
    }

    [Fact]
    public void BuildBackgroundGraph_Fills_The_Frame_For_1080p()
    {
        var graph = VinylFilterGraphBuilder.BuildBackgroundGraph(VideoPreset.FullHd1080p);

        graph.Should().StartWith("color=c=black:s=1920x1080");
        graph.Should().EndWith("[background]");
    }

    [Fact]
    public void BuildBackgroundGraph_Fills_The_Frame_For_720p()
    {
        var graph = VinylFilterGraphBuilder.BuildBackgroundGraph(VideoPreset.Hd720p);

        graph.Should().StartWith("color=c=black:s=1280x720");
        graph.Should().EndWith("[background]");
    }

    /// <summary>
    /// The backdrop is the app's page, not the artwork: it reads no input, and its glow is centred
    /// where the page's is, at 20%/20% of the frame.
    /// </summary>
    [Fact]
    public void BuildBackgroundGraph_Is_The_Page_Glow_Rather_Than_The_Artwork()
    {
        var graph = VinylFilterGraphBuilder.BuildBackgroundGraph(VideoPreset.FullHd1080p);

        graph.Should().NotContain("[0:v]");
        graph.Should().Contain("hypot(X-384,Y-216)");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Starts_From_Input_Zero_And_Ends_With_The_Final_Pad()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(VideoPreset.FullHd1080p, "Title", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().StartWith("[0:v]rotate=");
        graph.Should().EndWith("[final]");
    }

    /// <summary>
    /// The caption must reach drawtext as a file reference, never as inline <c>text=</c>. An
    /// earlier version inlined it with escaping, which looked correct in a unit test but was
    /// rejected by ffmpeg for any title containing an apostrophe - ffmpeg does not honour
    /// <c>\'</c> inside a single-quoted option, so the title escaped its own option and injected
    /// into the graph. Asserting the absence of <c>text=</c> pins the structural fix in place;
    /// FfmpegVideoRendererTests proves the behaviour against real ffmpeg.
    /// </summary>
    [Fact]
    public void BuildRotatingCompositeGraph_Passes_The_Caption_By_File_Rather_Than_Inline_Text()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(
            VideoPreset.FullHd1080p, "/tmp/jobs/title.txt", FontFilePath, rotationPeriodSeconds: 2.0);

        graph.Should().Contain("textfile='/tmp/jobs/title.txt'");
        graph.Should().Contain("expansion=none");
        // ":text=" is the inline-caption option; matching on the bare "text=" would also hit the
        // "drawtext=" filter name itself.
        graph.Should().NotContain(":text=");
    }

    [Fact]
    public void BuildRotatingCompositeGraph_Escapes_Windows_Style_Paths_Backslashes_First()
    {
        var graph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(
            VideoPreset.FullHd1080p, @"C:\jobs\title.txt", @"C:\Windows\Fonts\arialbd.ttf", rotationPeriodSeconds: 2.0);

        graph.Should().Contain(@"textfile='C\:\\jobs\\title.txt'");
        graph.Should().Contain(@"fontfile='C\:\\Windows\\Fonts\\arialbd.ttf'");
    }
}
