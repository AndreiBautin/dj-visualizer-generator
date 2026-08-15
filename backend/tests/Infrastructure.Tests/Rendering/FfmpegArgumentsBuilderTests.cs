using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Rendering;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

public class FfmpegArgumentsBuilderTests
{
    private static readonly RenderRequest Request = new(
        AudioFilePath: "/jobs/x/input/audio.mp3",
        ArtworkFilePath: "/jobs/x/input/artwork.png",
        OutputFilePath: "/jobs/x/output/video.mp4",
        Preset: VideoPreset.FullHd1080p,
        Title: "Test Mix",
        Duration: TimeSpan.FromMinutes(45),
        RotationPeriodSeconds: 3.0,
        CaptionFont: CaptionFont.SansBold);

    [Fact]
    public void BuildStaticVinylArguments_Outputs_A_Single_Frame_To_The_Vinyl_Image_Path()
    {
        var args = FfmpegArgumentsBuilder.BuildStaticVinylArguments(Request, "filtergraph", "/jobs/x/.vinyl.png");

        args.Should().ContainInConsecutiveOrder("-frames:v", "1");
        args.Should().Contain("-update");
        args.Last().Should().Be("/jobs/x/.vinyl.png");
    }

    [Theory]
    [InlineData(2.0, 30, 2.0)] // already frame-aligned (60 frames)
    [InlineData(3.0, 30, 3.0)] // already frame-aligned (90 frames)
    [InlineData(2.01, 30, 2.0)] // 60.3 frames rounds down to 60 -> 2.0s
    [InlineData(0.01, 30, 1.0 / 30)] // 0.3 frames rounds to 0, clamped up to 1 frame
    public void SnapRotationPeriodToFrames_Rounds_To_A_Whole_Number_Of_Frames(double requested, int frameRate, double expected)
    {
        var snapped = FfmpegArgumentsBuilder.SnapRotationPeriodToFrames(requested, frameRate);

        (snapped * frameRate).Should().BeApproximately(Math.Round(snapped * frameRate), 1e-9);
        snapped.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void SnapRotationPeriodToFrames_Never_Rounds_Down_To_Zero_Frames()
    {
        var snapped = FfmpegArgumentsBuilder.SnapRotationPeriodToFrames(0.001, 30);

        snapped.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildLoopSegmentArguments_Renders_Exactly_The_Given_Loop_Duration_From_The_Vinyl_Image()
    {
        var args = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            "/jobs/x/.vinyl.png", "filtergraph", "libx264", "veryfast", loopDurationSeconds: 3.0, "/jobs/x/.loop.mp4");

        args.Should().ContainInConsecutiveOrder("-loop", "1", "-i", "/jobs/x/.vinyl.png");
        args.Should().ContainInConsecutiveOrder("-t", "3");
        args.Last().Should().Be("/jobs/x/.loop.mp4");
    }

    [Fact]
    public void BuildLoopSegmentArguments_Uses_Libx264_With_The_Configured_Preset_By_Default()
    {
        var args = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            "/jobs/x/.vinyl.png", "filtergraph", "libx264", "veryfast", loopDurationSeconds: 3.0, "/jobs/x/.loop.mp4");

        args.Should().ContainInConsecutiveOrder("-c:v", "libx264");
        args.Should().ContainInConsecutiveOrder("-preset", "veryfast");
    }

    [Fact]
    public void BuildLoopSegmentArguments_Uses_Nvenc_When_Selected()
    {
        var args = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            "/jobs/x/.vinyl.png", "filtergraph", "h264_nvenc", "veryfast", loopDurationSeconds: 3.0, "/jobs/x/.loop.mp4");

        args.Should().ContainInConsecutiveOrder("-c:v", "h264_nvenc");
        args.Should().NotContain("libx264");
        args.Should().NotContain("veryfast");
    }

    [Fact]
    public void BuildMuxArguments_Loops_The_Segment_Indefinitely_And_Copies_The_Video_Stream()
    {
        var args = FfmpegArgumentsBuilder.BuildMuxArguments("/jobs/x/.loop.mp4", Request);

        // -c:v copy is the whole point: no video re-encoding for the full (possibly hours-long)
        // duration, only cheap container-level repackaging of the looped segment's bytes.
        args.Should().ContainInConsecutiveOrder("-stream_loop", "-1", "-i", "/jobs/x/.loop.mp4");
        args.Should().ContainInConsecutiveOrder("-i", "/jobs/x/input/audio.mp3");
        args.Should().ContainInConsecutiveOrder("-c:v", "copy");
        args.Should().ContainInConsecutiveOrder("-map", "0:v");
        args.Should().ContainInConsecutiveOrder("-map", "1:a");
        args.Should().Contain("-shortest");
        args.Last().Should().Be(Request.OutputFilePath);
    }

    [Fact]
    public void BuildMuxArguments_Caps_Output_Duration_Explicitly_At_The_Requests_Real_Duration()
    {
        // -shortest alone is not reliable here: -stream_loop -1 combined with -c:v copy can leave
        // ffmpeg's shortest-stream bookkeeping confused about how much video time it has actually
        // written per loop, letting it overshoot the audio's real end by (empirically observed)
        // roughly a minute or more, regardless of the audio's length. An explicit -t using the
        // duration already known from probing the audio makes the cutoff exact and deterministic
        // instead of depending on that inference.
        var args = FfmpegArgumentsBuilder.BuildMuxArguments("/jobs/x/.loop.mp4", Request);

        args.Should().ContainInConsecutiveOrder("-t", "2700");
    }
}
