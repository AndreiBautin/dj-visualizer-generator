using System.Globalization;
using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>Pure ffmpeg argument-list builders, kept separate from process management so the
/// exact CLI arguments (and performance-relevant knobs like the encoder/preset) are directly
/// unit-testable without invoking ffmpeg.</summary>
internal static class FfmpegArgumentsBuilder
{
    public const int FrameRate = 30;

    public static IReadOnlyList<string> BuildStaticVinylArguments(RenderRequest request, string filterGraph, string vinylImagePath) =>
    [
        "-y",
        // A second bound behind the upload check (ImageDimensions): the decoder refuses anything
        // over 8192x8192 rather than allocating for it.
        "-max_pixels", "67108864",
        .. FfmpegInputFormat.For(request.ArtworkFilePath),
        "-i", request.ArtworkFilePath,
        "-filter_complex", filterGraph,
        "-map", "[vinyl_static]",
        "-frames:v", "1",
        "-update", "1",
        "-loglevel", "error",
        vinylImagePath,
    ];

    /// <summary>The backdrop graph is its own source, so this pass reads no input file.</summary>
    public static IReadOnlyList<string> BuildBackgroundArguments(string filterGraph, string backgroundImagePath) =>
    [
        "-y",
        "-filter_complex", filterGraph,
        "-map", "[background]",
        "-frames:v", "1",
        "-update", "1",
        "-loglevel", "error",
        backgroundImagePath,
    ];

    /// <summary>
    /// Rounds a requested rotation period to the nearest whole number of frames at
    /// <paramref name="frameRate"/> (minimum one frame), so a looped render never has a visible
    /// jump where it wraps back to frame 0 - the rotation angle at the last frame of one loop is
    /// then guaranteed to lead smoothly into the first frame of the next.
    /// </summary>
    public static double SnapRotationPeriodToFrames(double requestedPeriodSeconds, int frameRate)
    {
        var frameCount = Math.Max(1, (int)Math.Round(requestedPeriodSeconds * frameRate));
        return frameCount / (double)frameRate;
    }

    /// <summary>
    /// Renders exactly <paramref name="loopDurationSeconds"/> (already frame-snapped by the
    /// caller via <see cref="SnapRotationPeriodToFrames"/>) of the spinning vinyl - the only
    /// unique frames that ever need generating, since the rotation is perfectly periodic and the
    /// rest of the video is this clip looped, not re-rendered.
    /// </summary>
    /// <param name="videoCodec">"libx264" (portable, works everywhere including Docker/CI with no
    /// GPU) or "h264_nvenc" (NVIDIA hardware encoding - much faster, but only available on
    /// machines with a compatible NVIDIA GPU and driver).</param>
    /// <param name="x264Preset">Only used when <paramref name="videoCodec"/> is "libx264" - nvenc
    /// uses its own preset scheme and gets a fixed balanced preset instead.</param>
    public static IReadOnlyList<string> BuildLoopSegmentArguments(
        string vinylImagePath,
        string backgroundImagePath,
        string filterGraph,
        string videoCodec,
        string x264Preset,
        double loopDurationSeconds,
        string loopSegmentPath,
        int encoderThreads = 0,
        int x264Crf = 0)
    {
        // 0 leaves both thread counts to ffmpeg; see WorkerOptions.EncoderThreads.
        var threads = encoderThreads.ToString(CultureInfo.InvariantCulture);
        var filterThreadArgs = encoderThreads > 0 ? new[] { "-filter_threads", threads } : [];
        var encoderThreadArgs = encoderThreads > 0 ? new[] { "-threads", threads } : [];

        var encoderArgs = videoCodec == "h264_nvenc"
            ? new[] { "-c:v", "h264_nvenc", "-preset", "p4", "-rc", "vbr", "-cq", "23" }
            : x264Crf > 0
                ? new[] { "-c:v", "libx264", "-preset", x264Preset, "-crf", x264Crf.ToString(CultureInfo.InvariantCulture) }
                : new[] { "-c:v", "libx264", "-preset", x264Preset };

        return
        [
            "-y",
            .. filterThreadArgs,
            "-loop", "1",
            "-i", vinylImagePath,
            "-loop", "1",
            "-i", backgroundImagePath,
            "-filter_complex", filterGraph,
            "-map", "[final]",
            "-t", loopDurationSeconds.ToString(CultureInfo.InvariantCulture),
            .. encoderArgs,
            .. encoderThreadArgs,
            "-pix_fmt", "yuv420p",
            "-r", FrameRate.ToString(CultureInfo.InvariantCulture),
            // This pass encodes a known duration, so it can report real progress rather than only
            // marking its own completion. It is also the slowest of the three duration-independent
            // passes, which is what made the bar look stuck on a constrained host.
            "-progress", "pipe:1",
            "-nostats",
            "-loglevel", "error",
            loopSegmentPath,
        ];
    }

    public const string ReencodedAudioBitrate = "192k";

    /// <summary>Whether an input audio stream with this ffprobe <c>codec_name</c> can be copied into the MP4 unchanged.</summary>
    public static bool CanCopyAudioCodec(string? codecName) => string.Equals(codecName, "aac", StringComparison.Ordinal);

    /// <summary>
    /// Loops the short rendered segment indefinitely and muxes in the real audio, stopping at the
    /// audio's actual duration. Uses <c>-c:v copy</c> - the video stream's already-encoded bytes
    /// are just repackaged, not re-encoded, so this step's cost is bounded by I/O rather than by
    /// the output's duration.
    /// </summary>
    /// <remarks><c>-shortest</c> alone is not reliable when combined with <c>-stream_loop -1</c>
    /// and <c>-c:v copy</c>: ffmpeg's shortest-stream bookkeeping can lose track of how much video
    /// time it has actually written per loop and overshoot the audio's real end by a large,
    /// non-proportional amount. <c>-t</c>, using the duration already known from probing the
    /// audio at upload time, makes the cutoff exact instead of depending on that inference -
    /// <c>-shortest</c> is kept only as a harmless secondary bound.
    /// <para>Audio that is already AAC is copied rather than re-encoded: re-encoding was most of a
    /// long render's wall time on the free instance (about 1,050 s of a 40-minute mix's 21.6
    /// minutes). Anything else is encoded at <see cref="ReencodedAudioBitrate"/>. MP3 is not
    /// copied even though MP4 can hold it, because Safari playback of MP3-in-MP4 is unverified.</para>
    /// <para><c>+faststart</c> moves the index to the front so the preview can start playing without
    /// first fetching the end of a several-hundred-megabyte file.</para></remarks>
    public static IReadOnlyList<string> BuildMuxArguments(string loopSegmentPath, RenderRequest request, bool copyAudio) =>
    [
        "-y",
        "-stream_loop", "-1",
        "-i", loopSegmentPath,
        .. FfmpegInputFormat.For(request.AudioFilePath),
        "-i", request.AudioFilePath,
        "-map", "0:v",
        "-map", "1:a",
        "-c:v", "copy",
        .. (copyAudio ? ["-c:a", "copy"] : new[] { "-c:a", "aac", "-b:a", ReencodedAudioBitrate }),
        "-t", request.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture),
        "-shortest",
        "-movflags", "+faststart",
        "-progress", "pipe:1",
        "-nostats",
        "-loglevel", "error",
        request.OutputFilePath,
    ];
}
