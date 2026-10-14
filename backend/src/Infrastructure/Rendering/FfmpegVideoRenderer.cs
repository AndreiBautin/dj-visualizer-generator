using System.Text;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Processes;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Renders in four ffmpeg passes rather than one continuous encode, because most of the work a
/// naive single-pass render would do is redundant:
///
/// 1. Draw the record - grooved disc, the artwork as its label, the spindle hole - as a single
///    static image (the expensive per-pixel <c>geq</c> math runs exactly once, not per frame).
/// 2. Draw the backdrop - black with the page's accent glow, and the title - also as one static image.
/// 3. Render exactly one rotation period of the record spinning over that
///    background - the rotation is perfectly periodic, so these are the
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
    int encoderThreads = 0,
    int x264Crf = 0,
    string ffmpegPath = "ffmpeg",
    string ffprobePath = "ffprobe") : IVideoRenderer
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
            // graph - see VinylFilterGraphBuilder.BuildBackgroundGraph for why. UTF-8
            // without a BOM: drawtext would otherwise render the BOM as a visible glyph.
            // A title is optional; with none there is no caption file and no drawtext stage.
            var hasCaption = !string.IsNullOrWhiteSpace(request.Title);
            if (hasCaption)
            {
                await File.WriteAllTextAsync(titleFilePath, request.Title, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            }

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

            await RenderBackgroundAsync(request, fontFilePath, hasCaption ? titleFilePath : null, backgroundImagePath, cancellationToken);
            await ReportAsync(RenderProgressScale.BackgroundComplete, cancellationToken);

            // Probed before the rotation pass because it decides where that pass hands the bar over.
            var copyAudio = FfmpegArgumentsBuilder.CanCopyAudioCodec(await ProbeAudioCodecAsync(request.AudioFilePath, cancellationToken));
            var loopSegmentEnd = RenderProgressScale.LoopSegmentEndFor(request.Duration, copyAudio);

            await RenderLoopSegmentAsync(request, vinylImagePath, backgroundImagePath, loopSegmentPath, loopSegmentEnd, ReportAsync, cancellationToken);
            await MuxFinalVideoAsync(request, loopSegmentPath, copyAudio, loopSegmentEnd, ReportAsync, cancellationToken);

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
        var filterGraph = VinylFilterGraphBuilder.BuildStaticVinylGraph(request.Preset, request.LabelCrop);
        var arguments = FfmpegArgumentsBuilder.BuildStaticVinylArguments(request, filterGraph, vinylImagePath);
        await RunFfmpegAsync(arguments, cancellationToken, onOutputLine: null);
    }

    private async Task RenderBackgroundAsync(RenderRequest request, string fontFilePath, string? titleFilePath, string backgroundImagePath, CancellationToken cancellationToken)
    {
        var filterGraph = VinylFilterGraphBuilder.BuildBackgroundGraph(request.Preset, titleFilePath, fontFilePath);
        var arguments = FfmpegArgumentsBuilder.BuildBackgroundArguments(filterGraph, backgroundImagePath);
        await RunFfmpegAsync(arguments, cancellationToken, onOutputLine: null);
    }

    private async Task RenderLoopSegmentAsync(
        RenderRequest request,
        string vinylImagePath,
        string backgroundImagePath,
        string loopSegmentPath,
        int loopSegmentEnd,
        RenderProgressCallback onProgress,
        CancellationToken cancellationToken)
    {
        var loopDurationSeconds = FfmpegArgumentsBuilder.SnapRotationPeriodToFrames(request.RotationPeriodSeconds, FfmpegArgumentsBuilder.FrameRate);
        var filterGraph = VinylFilterGraphBuilder.BuildRotatingCompositeGraph(request.Preset, loopDurationSeconds);
        var arguments = FfmpegArgumentsBuilder.BuildLoopSegmentArguments(
            vinylImagePath, backgroundImagePath, filterGraph, videoCodec, x264Preset, loopDurationSeconds, loopSegmentPath, encoderThreads, x264Crf);
        var loopDuration = TimeSpan.FromSeconds(loopDurationSeconds);

        await RunFfmpegAsync(arguments, cancellationToken, onOutputLine: async (line, ct) =>
        {
            if (FfmpegProgressParser.TryParseElapsed(line, out var elapsed))
            {
                await onProgress(RenderProgressScale.ForLoopSegment(FfmpegProgressParser.CalculatePercent(elapsed, loopDuration), loopSegmentEnd), ct);
            }
        });

        await onProgress(loopSegmentEnd, cancellationToken);
    }

    private async Task MuxFinalVideoAsync(
        RenderRequest request,
        string loopSegmentPath,
        bool copyAudio,
        int loopSegmentEnd,
        RenderProgressCallback onProgress,
        CancellationToken cancellationToken)
    {
        var arguments = FfmpegArgumentsBuilder.BuildMuxArguments(loopSegmentPath, request, copyAudio);

        // No local de-duplication: the shared gate in RenderAsync already drops repeats, and doing
        // it here as well would only hide which pass a value came from.
        await RunFfmpegAsync(arguments, cancellationToken, onOutputLine: async (line, ct) =>
        {
            if (FfmpegProgressParser.TryParseElapsed(line, out var elapsed))
            {
                await onProgress(RenderProgressScale.ForMux(FfmpegProgressParser.CalculatePercent(elapsed, request.Duration), loopSegmentEnd), ct);
            }
        });
    }

    /// <summary>
    /// The first audio stream's codec name, or null when it cannot be determined. Null only ever
    /// costs speed: the caller then re-encodes, which is always correct.
    /// </summary>
    private async Task<string?> ProbeAudioCodecAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        string[] arguments = ["-v", "error", .. FfmpegInputFormat.For(audioFilePath), "-select_streams", "a:0", "-show_entries", "stream=codec_name", "-of", "default=noprint_wrappers=1:nokey=1", audioFilePath];
        try
        {
            var result = await ProcessRunner.RunAsync(ffprobePath, arguments, cancellationToken);
            return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        }
        catch (ProcessRunner.StartException)
        {
            return null;
        }
    }

    private string ResolveFontPath(CaptionFont captionFont) =>
        fontFilePaths.TryGetValue(captionFont, out var path)
            ? path
            : throw new RenderException($"No font file is configured for caption font '{captionFont}'.");

    private async Task RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task>? onOutputLine)
    {
        ProcessResult result;
        try
        {
            // Output is always consumed: discarded when nobody wants the progress lines, so a
            // chatty pass can never fill the pipe and stall.
            result = await ProcessRunner.RunAsync(ffmpegPath, arguments, cancellationToken, onOutputLine ?? ((_, _) => Task.CompletedTask));
        }
        catch (ProcessRunner.StartException ex)
        {
            throw new RenderException(ex.Message);
        }

        if (result.ExitCode != 0)
        {
            throw new RenderException($"ffmpeg exited with code {result.ExitCode}: {result.StandardError}");
        }
    }
}
