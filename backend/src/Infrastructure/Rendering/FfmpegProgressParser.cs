namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>Parses lines from ffmpeg's <c>-progress pipe:1</c> output. Uses out_time_us (always
/// microseconds) rather than the confusingly-named out_time_ms (also microseconds, a long-standing
/// ffmpeg naming quirk) to avoid ambiguity.</summary>
internal static class FfmpegProgressParser
{
    private const string OutTimeUsPrefix = "out_time_us=";

    public static bool TryParseElapsed(string line, out TimeSpan elapsed)
    {
        elapsed = default;

        if (!line.StartsWith(OutTimeUsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(line.AsSpan(OutTimeUsPrefix.Length), out var microseconds))
        {
            return false;
        }

        elapsed = TimeSpan.FromMicroseconds(microseconds);
        return true;
    }

    public static bool IsEndOfProgress(string line) => line == "progress=end";

    public static int CalculatePercent(TimeSpan elapsed, TimeSpan totalDuration)
    {
        if (totalDuration <= TimeSpan.Zero)
        {
            return 0;
        }

        var ratio = elapsed.TotalSeconds / totalDuration.TotalSeconds;
        return Math.Clamp((int)Math.Round(ratio * 100), 0, 100);
    }
}
