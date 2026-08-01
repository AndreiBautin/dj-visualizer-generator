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
