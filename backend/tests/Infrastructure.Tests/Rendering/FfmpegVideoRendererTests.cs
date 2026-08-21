using System.Diagnostics;
using System.Text.Json;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Rendering;
using DjVisualizer.Infrastructure.Tests.Audio;
using DjVisualizer.Infrastructure.Tests.Support;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

public class FfmpegVideoRendererTests : IDisposable
{
    // ffmpeg's drawtext filter needs a real font file. In CI/Docker (Linux), fonts-dejavu-core
    // provides the first path; on a Windows dev machine that path doesn't exist and Fontconfig
    // fails to initialize entirely, so fall back to a font Windows always ships with.
    private static readonly string FontFilePath = OperatingSystem.IsWindows()
        ? @"C:\Windows\Fonts\arialbd.ttf"
        : "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    private static readonly IReadOnlyDictionary<CaptionFont, string> FontFilePaths = new Dictionary<CaptionFont, string>
    {
        [CaptionFont.SansBold] = FontFilePath,
        [CaptionFont.SerifBold] = FontFilePath,
        [CaptionFont.MonoBold] = FontFilePath,
    };

    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"djvisualizer-render-{Guid.NewGuid():N}");

    public FfmpegVideoRendererTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [RequiresFfmpegFact]
    public async Task RenderAsync_Produces_An_Mp4_Matching_The_Requested_Preset_And_Duration()
    {
        var audioPath = Path.Combine(_workDir, "audio.wav");
        var artworkPath = Path.Combine(_workDir, "artwork.png");
        var outputPath = Path.Combine(_workDir, "video.mp4");
        var duration = TimeSpan.FromSeconds(2);
        File.WriteAllBytes(audioPath, SilentWavBuilder.Build(duration, sampleRate: 8000));
        await RunFfmpegAsync("-y", "-f", "lavfi", "-i", "color=c=red:s=64x64", "-frames:v", "1", artworkPath);

        var sut = new FfmpegVideoRenderer(FontFilePaths);
        var progressReports = new List<int>();
        var request = new RenderRequest(
            audioPath, artworkPath, outputPath, VideoPreset.Hd720p, "Test Mix: 100%", duration,
            RotationPeriodSeconds: 2.0, CaptionFont: CaptionFont.SansBold);

        await sut.RenderAsync(request, (percent, _) =>
        {
            progressReports.Add(percent);
            return Task.CompletedTask;
        }, CancellationToken.None);

        File.Exists(outputPath).Should().BeTrue();
        progressReports.Should().Contain(100);

        var probe = await ProbeVideoAsync(outputPath);
        probe.Width.Should().Be(1280);
        probe.Height.Should().Be(720);
        probe.CodecName.Should().Be("h264");
        probe.DurationSeconds.Should().BeApproximately(duration.TotalSeconds, 0.5);

        // The two-pass render (static vinyl composite, then rotate-only per frame) should leave
        // no intermediate files behind in the job's output directory.
        Directory.GetFiles(_workDir).Should().BeEquivalentTo([audioPath, artworkPath, outputPath]);
    }

    [RequiresFfmpegFact]
    public async Task RenderAsync_Throws_A_RenderException_When_Ffmpeg_Fails()
    {
        var sut = new FfmpegVideoRenderer(FontFilePaths);
        var request = new RenderRequest(
            Path.Combine(_workDir, "missing-audio.wav"),
            Path.Combine(_workDir, "missing-artwork.png"),
            Path.Combine(_workDir, "video.mp4"),
            VideoPreset.Hd720p,
            "Test",
            TimeSpan.FromSeconds(2),
            RotationPeriodSeconds: 2.0,
            CaptionFont: CaptionFont.SansBold);

        var act = () => sut.RenderAsync(request, (_, _) => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<RenderException>();
    }

    [Fact]
    public async Task RenderAsync_Fails_Fast_When_No_Font_Is_Configured_For_The_Requested_CaptionFont()
    {
        // No ffmpeg needed for this one: an unconfigured font is caught before any process starts.
        var incompleteFontMap = new Dictionary<CaptionFont, string> { [CaptionFont.SansBold] = FontFilePath };
        var sut = new FfmpegVideoRenderer(incompleteFontMap);
        var request = new RenderRequest(
            Path.Combine(_workDir, "audio.wav"),
            Path.Combine(_workDir, "artwork.png"),
            Path.Combine(_workDir, "video.mp4"),
            VideoPreset.Hd720p,
            "Test",
            TimeSpan.FromSeconds(2),
            RotationPeriodSeconds: 2.0,
            CaptionFont: CaptionFont.MonoBold);

        var act = () => sut.RenderAsync(request, (_, _) => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<RenderException>();
    }

    /// <summary>
    /// Progress must actually move while the early passes run. Only the last two ffmpeg passes can
    /// report from inside themselves, so before this the bar sat at 0 until the mux started - on a
    /// constrained host that was about two thirds of the render, and it read as hung.
    /// Deliberately asserts against a real render rather than a stubbed callback, because what
    /// matters is that ffmpeg genuinely emits progress for the rotation pass.
    /// </summary>
    [RequiresFfmpegFact]
    public async Task RenderAsync_Reports_Progress_Before_The_Final_Mux_Pass_Begins()
    {
        var audioPath = Path.Combine(_workDir, "audio.wav");
        var artworkPath = Path.Combine(_workDir, "artwork.png");
        var outputPath = Path.Combine(_workDir, "video.mp4");
        var duration = TimeSpan.FromSeconds(3);
        File.WriteAllBytes(audioPath, SilentWavBuilder.Build(duration, sampleRate: 8000));
        await RunFfmpegAsync("-y", "-f", "lavfi", "-i", "color=c=red:s=64x64", "-frames:v", "1", artworkPath);

        var reported = new List<int>();
        var request = new RenderRequest(
            audioPath, artworkPath, outputPath, VideoPreset.Hd720p, "Progress", duration,
            RotationPeriodSeconds: 2.0, CaptionFont: CaptionFont.SansBold);

        await new FfmpegVideoRenderer(FontFilePaths).RenderAsync(request, (percent, _) =>
        {
            reported.Add(percent);
            return Task.CompletedTask;
        }, CancellationToken.None);

        reported.Should().NotBeEmpty();
        reported.Should().BeInAscendingOrder("a progress bar must never move backwards");
        reported.Should().OnlyHaveUniqueItems("each report costs a status.json write, so repeats are dropped");
        reported.Should().AllSatisfy(percent => percent.Should().BeInRange(0, 100));

        // The substance of the fix: something is reported while the static and rotation passes
        // run, not only once the mux begins.
        reported.Should().Contain(percent => percent > 0 && percent < RenderProgressScale.MuxStart,
            "the passes before the mux should move the bar");
        reported.Should().Contain(RenderProgressScale.LoopSegmentEnd);
        reported.Last().Should().Be(100, "the bar must finish even if ffmpeg undershoots the duration");
    }

    /// <summary>
    /// The job title is caller-controlled text that ends up inside a single-quoted ffmpeg
    /// drawtext option, which ffmpeg's filtergraph parser then re-parses - so a stray quote or
    /// backslash is a filter-graph injection risk, not just a cosmetic bug. This renders for
    /// real rather than asserting on the graph string, because only ffmpeg itself decides
    /// whether the escaping is actually correct.
    /// </summary>
    [RequiresFfmpegTheory]
    [InlineData("It's a Mix")]
    [InlineData(@"Back\slash")]
    [InlineData("Drum:Bass 100%")]
    [InlineData(@"Quote'Colon:Percent%Back\slash")]
    [InlineData("':drawtext=text=pwned:x=0:y=0:'")]
    [InlineData("[0:v]split[a][b];[a]nullsink[c]")]
    public async Task RenderAsync_Handles_Titles_Containing_Filtergraph_Metacharacters(string hostileTitle)
    {
        var audioPath = Path.Combine(_workDir, "audio.wav");
        var artworkPath = Path.Combine(_workDir, "artwork.png");
        var outputPath = Path.Combine(_workDir, $"video-{Guid.NewGuid():N}.mp4");
        var duration = TimeSpan.FromSeconds(1);
        File.WriteAllBytes(audioPath, SilentWavBuilder.Build(duration, sampleRate: 8000));
        await RunFfmpegAsync("-y", "-f", "lavfi", "-i", "color=c=red:s=64x64", "-frames:v", "1", artworkPath);

        var sut = new FfmpegVideoRenderer(FontFilePaths);
        var request = new RenderRequest(
            audioPath, artworkPath, outputPath, VideoPreset.Hd720p, hostileTitle, duration,
            RotationPeriodSeconds: 1.0, CaptionFont: CaptionFont.SansBold);

        var render = async () => await sut.RenderAsync(request, (_, _) => Task.CompletedTask, CancellationToken.None);

        await render.Should().NotThrowAsync();
        File.Exists(outputPath).Should().BeTrue();
    }

    private static async Task RunFfmpegAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("ffmpeg") { UseShellExecute = false };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(0);
    }

    private static async Task<(int Width, int Height, string CodecName, double DurationSeconds)> ProbeVideoAsync(string path)
    {
        var startInfo = new ProcessStartInfo("ffprobe")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-select_streams");
        startInfo.ArgumentList.Add("v:0");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("stream=width,height,codec_name:format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(path);

        using var process = Process.Start(startInfo)!;
        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var document = JsonDocument.Parse(json);
        var stream = document.RootElement.GetProperty("streams")[0];
        var width = stream.GetProperty("width").GetInt32();
        var height = stream.GetProperty("height").GetInt32();
        var codecName = stream.GetProperty("codec_name").GetString()!;
        var durationSeconds = double.Parse(document.RootElement.GetProperty("format").GetProperty("duration").GetString()!);

        return (width, height, codecName, durationSeconds);
    }
}
