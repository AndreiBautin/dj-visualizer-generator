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
        "-i", request.ArtworkFilePath,
        "-filter_complex", filterGraph,
        "-map", "[vinyl_static]",
        "-frames:v", "1",
        "-update", "1",
        "-loglevel", "error",
        vinylImagePath,
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
        string filterGraph,
        string videoCodec,
        string x264Preset,
        double loopDurationSeconds,
        string loopSegmentPath)
    {
        var encoderArgs = videoCodec == "h264_nvenc"
            ? new[] { "-c:v", "h264_nvenc", "-preset", "p4", "-rc", "vbr", "-cq", "23" }
            : new[] { "-c:v", "libx264", "-preset", x264Preset };

        return
        [
            "-y",
            "-loop", "1",
            "-i", vinylImagePath,
            "-filter_complex", filterGraph,
            "-map", "[final]",
            "-t", loopDurationSeconds.ToString(CultureInfo.InvariantCulture),
            .. encoderArgs,
            "-pix_fmt", "yuv420p",
            "-r", FrameRate.ToString(CultureInfo.InvariantCulture),
            "-loglevel", "error",
            loopSegmentPath,
        ];
    }

    /// <summary>
    /// Loops the short rendered segment indefinitely and muxes in the real audio, stopping at the
    /// audio's actual duration. Uses <c>-c:v copy</c> - the video stream's already-encoded bytes
    /// are just repackaged, not re-encoded, so this step's cost is bounded by I/O rather than by
    /// the output's duration.
    /// </summary>
    public static IReadOnlyList<string> BuildMuxArguments(string loopSegmentPath, RenderRequest request) =>
    [
        "-y",
        "-stream_loop", "-1",
        "-i", loopSegmentPath,
        "-i", request.AudioFilePath,
        "-map", "0:v",
        "-map", "1:a",
        "-c:v", "copy",
        "-c:a", "aac",
        "-b:a", "320k",
        "-shortest",
        "-progress", "pipe:1",
        "-nostats",
        "-loglevel", "error",
        request.OutputFilePath,
    ];
}
