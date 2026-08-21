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
/// The weights are a deliberate compromise and cannot be right for every input, because the
/// passes scale with different things: passes 1-3 scale with output resolution and are
/// independent of the mix's length, while the mux is a stream copy whose cost tracks the audio
/// duration. A twenty-second sample is dominated by the static passes; a six-hour set is
/// dominated by the mux. Splitting the difference keeps the bar moving in both cases, which is
/// the point - a progress bar's job is to show that work is happening, not to predict when it
/// will stop.
/// </para>
/// </remarks>
internal static class RenderProgressScale
{
    /// <summary>Reported once the circular artwork still is written.</summary>
    public const int StaticVinylComplete = 5;

    /// <summary>Reported once the blurred ambient background still is written.</summary>
    public const int AmbientBackgroundComplete = 10;

    public const int LoopSegmentStart = AmbientBackgroundComplete;

    /// <summary>The rotation pass encodes a known duration, so it reports real progress across
    /// this span rather than only marking its completion.</summary>
    public const int LoopSegmentEnd = 45;

    public const int MuxStart = LoopSegmentEnd;

    public static int ForLoopSegment(int passPercent) => Scale(passPercent, LoopSegmentStart, LoopSegmentEnd);

    public static int ForMux(int passPercent) => Scale(passPercent, MuxStart, 100);

    /// <summary>Clamps before scaling: a pass reporting something nonsensical must not push the
    /// overall value outside 0-100, which <c>Job.UpdateProgress</c> rejects outright and would
    /// fail an otherwise healthy render.</summary>
    private static int Scale(int passPercent, int start, int end)
    {
        var clamped = Math.Clamp(passPercent, 0, 100);
        return start + (int)Math.Round(clamped / 100.0 * (end - start));
    }
}
