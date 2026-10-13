namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Maps each of the four render passes onto a slice of the single 0-100 bar the client sees.
/// </summary>
/// <remarks>
/// <para>
/// Only the last two passes can report progress from inside themselves - the first two produce a
/// single frame each, so there is nothing to report against. Without this mapping the bar stayed
/// at 0 until the mux began, which on a constrained host meant roughly two thirds of the render
/// elapsed with no visible movement and the page read as hung.
/// </para>
/// <para>
/// The passes scale with different things: passes 1-3 with output resolution, independent of
/// the length of the mix, while the cost of the mux tracks the audio duration. A short sample is
/// dominated by the static passes and a long set by the mux, so the hand-over point between the
/// rotation pass and the mux moves with the duration (<see cref="LoopSegmentEndFor"/>), keeping
/// the speed of the bar roughly even instead of fast-then-crawling.
/// </para>
/// </remarks>
internal static class RenderProgressScale
{
    /// <summary>Reported once the record still is written.</summary>
    public const int StaticVinylComplete = 5;

    /// <summary>Reported once the backdrop still is written.</summary>
    public const int BackgroundComplete = 10;

    public const int LoopSegmentStart = BackgroundComplete;

    /// <summary>Seconds of audio whose mux costs about as much as the rotation pass. Measured on
    /// the free host (2026-10-14): a 40-minute mix spent about 250 s in the rotation pass and
    /// about 1,050 s muxing - the mux ran at about 0.44 s per audio second against a roughly
    /// fixed rotation pass. With the old fixed 45/55 split that render crawled at 1% per 20 s
    /// from halfway and read as stalled.</summary>
    private const double MuxBalanceSeconds = 480;

    /// <summary>Where the rotation pass hands over to the mux: late for a short sample (the mux is
    /// trivial), early for a long set (the mux is most of the work). Clamped so each pass always
    /// keeps a visible slice of the bar.</summary>
    public static int LoopSegmentEndFor(TimeSpan audioDuration)
    {
        var seconds = Math.Max(0, audioDuration.TotalSeconds);
        var muxShare = seconds / (seconds + MuxBalanceSeconds);
        var end = 100 - (int)Math.Round(muxShare * (100 - LoopSegmentStart));
        return Math.Clamp(end, LoopSegmentStart + 10, 90);
    }

    public static int ForLoopSegment(int passPercent, int loopSegmentEnd) => Scale(passPercent, LoopSegmentStart, loopSegmentEnd);

    public static int ForMux(int passPercent, int loopSegmentEnd) => Scale(passPercent, loopSegmentEnd, 100);

    /// <summary>Clamps before scaling: a pass reporting something nonsensical must not push the
    /// overall value outside 0-100, which <c>Job.UpdateProgress</c> rejects outright and would
    /// fail an otherwise healthy render.</summary>
    private static int Scale(int passPercent, int start, int end)
    {
        var clamped = Math.Clamp(passPercent, 0, 100);
        return start + (int)Math.Round(clamped / 100.0 * (end - start));
    }
}
