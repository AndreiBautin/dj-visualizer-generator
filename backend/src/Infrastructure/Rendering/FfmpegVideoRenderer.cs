using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Renders in four ffmpeg passes rather than one continuous encode, because most of the work a
/// naive single-pass render would do is redundant:
///
/// 1. Crop the artwork to a circle with a white border - a single static image (the expensive
///    per-pixel <c>geq</c> mask math runs exactly once, not per frame).
/// 2. Render a soft, blurred, darkened full-frame version of the same artwork as an ambient
///    background - also a single static image, so the blur costs nothing per output frame.
/// 3. Render exactly one rotation period of the disc (with its shadow) spinning over that
///    background, with the title overlaid - the rotation is perfectly periodic, so these are the
///    *only* visually unique frames that ever need generating, regardless of how long the final
///    video is. The requested period is snapped to a whole number of frames so the loop wraps
///    seamlessly.
/// 4. Loop that short clip to match the audio's real duration and mux the audio in, using
///    <c>-c:v copy</c> so the already-encoded video bytes are just repackaged rather than
///    re-encoded - this step's cost is bounded by I/O, not by the output's duration.
///
/// A long DJ set no longer costs proportionally more to render than a short one: steps 1-3 are
/// duration-independent, and step 4 does no video encoding at all.
/// </summary>
public sealed class FfmpegVideoRenderer(
    IReadOnlyDictionary<CaptionFont, string> fontFilePaths,
    string videoCodec = "libx264",
    string x264Preset = "veryfast",
    string ffmpegPath = "ffmpeg") : IVideoRenderer
{
    public async Task RenderAsync(RenderRequest request, RenderProgressCallback onProgress, CancellationToken cancellationToken)
    {
        // Resolved up front, before any ffmpeg process is started, so a misconfigured font choice
        // fails fast with a clear error instead of after wasting a real render pass.
        var fontFilePath = ResolveFontPath(request.CaptionFont);

        var workingDirectory = Path.GetDirectoryName(request.OutputFilePath) ?? ".";
        var vinylImagePath = Path.Combine(workingDirectory, $".vinyl-static-{Guid.NewGuid():N}.png");
        var backgroundImagePath = Path.Combine(workingDirectory, $".vinyl-background-{Guid.NewGuid():N}.png");
        var loopSegmentPath = Path.Combine(workingDirectory, $".vinyl-loop-{Guid.NewGuid():N}.mp4");

        try
        {
            await RenderStaticVinylAsync(request, vinylImagePath, cancellationToken);
            await RenderAmbientBackgroundAsync(request, backgroundImagePath, cancellationToken);
            await RenderLoopSegmentAsync(request, fontFilePath, vinylImagePath, backgroundImagePath, loopSegmentPath, cancellationToken);
            await MuxFinalVideoAsync(request, loopSegmentPath, onProgress, cancellationToken);
        }
        finally
        {
            foreach (var intermediateFile in new[] { vinylImagePath, backgroundImagePath, loopSegmentPath })
            {
                if (File.Exists(intermediateFile))
                {
                    File.Delete(intermediateFile);
                }
            }
        }
    }

    private async Task RenderStaticVinylAsync(RenderRequest request, string vinylImagePath, CancellationToken cancellationToken)
    {
        var filterGraph = VinylFilterGraphBuilder.BuildStaticVinylGraph(request.Preset);
        var arguments = FfmpegArgumentsBuilder.BuildStaticVinylArguments(request, filterGraph, vinylImagePath);
        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: false), cancellationToken, onOutputLine: null);
    }

    private async Task RenderAmbientBackgroundAsync(RenderRequest request, string backgroundImagePath, CancellationToken cancellationToken)
    {
        var filterGraph = VinylFilterGraphBuilder.BuildAmbientBackgroundGraph(request.Preset);
        var arguments = FfmpegArgumentsBuilder.BuildAmbientBackgroundArguments(request, filterGraph, backgroundImagePath);
        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: false), cancellationToken, onOutputLine: null);
    }

    private async Task RenderLoopSegmentAsync(
        RenderRequest request,
        string fontFilePath,
        string vinylImagePath,
        string backgroundImagePath,
        string loopSegmentPath,
        CancellationToken cancellationToken)
    {
        var loopDurationSeconds = FfmpegArgumentsBuilder.SnapRotationPeriodToFrames(request.RotationPeriodSeconds, FfmpegArgumentsBuilder.FrameRate);
        var filterGraph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(request.Preset, request.Title, fontFilePath, loopDurationSeconds);
        var arguments = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            vinylImagePath, backgroundImagePath, filterGraph, videoCodec, x264Preset, loopDurationSeconds, loopSegmentPath);
        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: false), cancellationToken, onOutputLine: null);
    }

    private async Task MuxFinalVideoAsync(
        RenderRequest request,
        string loopSegmentPath,
        RenderProgressCallback onProgress,
        CancellationToken cancellationToken)
    {
        var arguments = FfmpegArgumentsBuilder.BuildMuxArguments(loopSegmentPath, request);
        var lastReportedPercent = -1;

        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: true), cancellationToken, onOutputLine: async (line, ct) =>
        {
            if (!FfmpegProgressParser.TryParseElapsed(line, out var elapsed))
            {
                return;
            }

            var percent = FfmpegProgressParser.CalculatePercent(elapsed, request.Duration);
            if (percent == lastReportedPercent)
            {
                return;
            }

            lastReportedPercent = percent;
            await onProgress(percent, ct);
        });

        if (lastReportedPercent != 100)
        {
            await onProgress(100, cancellationToken);
        }
    }

    private string ResolveFontPath(CaptionFont captionFont) =>
        fontFilePaths.TryGetValue(captionFont, out var path)
            ? path
            : throw new RenderException($"No font file is configured for caption font '{captionFont}'.");

    private ProcessStartInfo BuildStartInfo(IReadOnlyList<string> arguments, bool redirectStandardOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    private static async Task RunFfmpegAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task>? onOutputLine)
    {
        using var process = new Process { StartInfo = startInfo };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new RenderException($"ffmpeg could not be started: {ex.Message}");
        }

        process.BeginErrorReadLine();

        try
        {
            if (onOutputLine is not null)
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
                {
                    await onOutputLine(line, cancellationToken);
                }
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new RenderException($"ffmpeg exited with code {process.ExitCode}: {stderr}");
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup during cancellation; the process may have already exited.
        }
    }
}
