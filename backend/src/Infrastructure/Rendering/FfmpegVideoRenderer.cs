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
        var titleFilePath = Path.Combine(workingDirectory, $".vinyl-title-{Guid.NewGuid():N}.txt");

        try
        {
            // The caption is handed to drawtext as a file rather than inlined into the filter
            // graph - see VinylFilterGraphBuilder.BuildRotatingCompositeGraph for why. UTF-8
            // without a BOM: drawtext would otherwise render the BOM as a visible glyph.
            await File.WriteAllTextAsync(titleFilePath, request.Title, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

            // One gate for every progress report, across all four passes. It enforces the two
            // properties the caller depends on: values never go backwards (each pass maps into a
            // slice above the previous one, but a pass can still emit the same mapped value
            // repeatedly), and an unchanged value is never re-reported - each report costs a
            // status.json write in ProcessRenderJobUseCase.
            var lastReportedPercent = -1;
            async Task ReportAsync(int percent, CancellationToken ct)
            {
                if (percent <= lastReportedPercent)
                {
                    return;
                }

                lastReportedPercent = percent;
                await onProgress(percent, ct);
            }

            await RenderStaticVinylAsync(request, vinylImagePath, cancellationToken);
            await ReportAsync(RenderProgressScale.StaticVinylComplete, cancellationToken);

            await RenderAmbientBackgroundAsync(request, backgroundImagePath, cancellationToken);
            await ReportAsync(RenderProgressScale.AmbientBackgroundComplete, cancellationToken);

            await RenderLoopSegmentAsync(request, fontFilePath, titleFilePath, vinylImagePath, backgroundImagePath, loopSegmentPath, ReportAsync, cancellationToken);
            await MuxFinalVideoAsync(request, loopSegmentPath, ReportAsync, cancellationToken);

            // The mux reports against the audio's duration, which ffmpeg can undershoot by a
            // fraction of a second; finish the bar explicitly rather than leaving it at 99.
            await ReportAsync(100, cancellationToken);
        }
        finally
        {
            foreach (var intermediateFile in new[] { vinylImagePath, backgroundImagePath, loopSegmentPath, titleFilePath })
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
        string titleFilePath,
        string vinylImagePath,
        string backgroundImagePath,
        string loopSegmentPath,
        RenderProgressCallback onProgress,
        CancellationToken cancellationToken)
    {
        var loopDurationSeconds = FfmpegArgumentsBuilder.SnapRotationPeriodToFrames(request.RotationPeriodSeconds, FfmpegArgumentsBuilder.FrameRate);
        var filterGraph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(request.Preset, titleFilePath, fontFilePath, loopDurationSeconds);
        var arguments = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            vinylImagePath, backgroundImagePath, filterGraph, videoCodec, x264Preset, loopDurationSeconds, loopSegmentPath);
        var loopDuration = TimeSpan.FromSeconds(loopDurationSeconds);

        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: true), cancellationToken, onOutputLine: async (line, ct) =>
        {
            if (FfmpegProgressParser.TryParseElapsed(line, out var elapsed))
            {
                await onProgress(RenderProgressScale.ForLoopSegment(FfmpegProgressParser.CalculatePercent(elapsed, loopDuration)), ct);
            }
        });

        await onProgress(RenderProgressScale.LoopSegmentEnd, cancellationToken);
    }

    private async Task MuxFinalVideoAsync(
        RenderRequest request,
        string loopSegmentPath,
        RenderProgressCallback onProgress,
        CancellationToken cancellationToken)
    {
        var arguments = FfmpegArgumentsBuilder.BuildMuxArguments(loopSegmentPath, request);

        // No local de-duplication: the shared gate in RenderAsync already drops repeats, and doing
        // it here as well would only hide which pass a value came from.
        await RunFfmpegAsync(BuildStartInfo(arguments, redirectStandardOutput: true), cancellationToken, onOutputLine: async (line, ct) =>
        {
            if (FfmpegProgressParser.TryParseElapsed(line, out var elapsed))
            {
                await onProgress(RenderProgressScale.ForMux(FfmpegProgressParser.CalculatePercent(elapsed, request.Duration)), ct);
            }
        });
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
