using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace DjVisualizer.Worker.Configuration;

/// <summary>
/// Builds <see cref="WorkerOptions"/> from raw configuration strings, the way JobsOptionsFactory
/// does for the API. Plain binding threw on the first malformed environment variable, crashing
/// the host; and it accepted values that break the worker quietly - a negative polling interval
/// throws out of <c>Task.Delay</c> and kills the polling loop, a zero cleanup interval spins, a
/// zero retention deletes videos before anyone can download them, an unknown preset only fails
/// at the first render. Every rejected value falls back to the documented default with a warning.
/// Parsing is pure and total: it never throws.
/// </summary>
public static class WorkerOptionsFactory
{
    private static readonly string[] X264Presets =
        ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo"];

    private static readonly string[] VideoCodecs = ["libx264", "h264_nvenc"];

    public static WorkerOptions Create(IConfiguration configuration, out IReadOnlyList<string> warnings)
    {
        var section = configuration.GetSection(WorkerOptions.SectionName);
        var collected = new List<string>();
        var defaults = new WorkerOptions();

        var options = new WorkerOptions
        {
            FontFilePathSansBold = Text(section["FontFilePathSansBold"], defaults.FontFilePathSansBold),
            FontFilePathSerifBold = Text(section["FontFilePathSerifBold"], defaults.FontFilePathSerifBold),
            FontFilePathMonoBold = Text(section["FontFilePathMonoBold"], defaults.FontFilePathMonoBold),
            X264Preset = OneOf(section["X264Preset"], X264Presets, defaults.X264Preset, "Worker:X264Preset", collected),
            VideoCodec = OneOf(section["VideoCodec"], VideoCodecs, defaults.VideoCodec, "Worker:VideoCodec", collected),
            EncoderThreads = Int(section["EncoderThreads"], defaults.EncoderThreads, minimum: 0, "Worker:EncoderThreads", collected),
            PollingIntervalSeconds = Int(section["PollingIntervalSeconds"], defaults.PollingIntervalSeconds, minimum: 1, "Worker:PollingIntervalSeconds", collected),
            CleanupIntervalSeconds = Int(section["CleanupIntervalSeconds"], defaults.CleanupIntervalSeconds, minimum: 1, "Worker:CleanupIntervalSeconds", collected),
            RetentionMinutes = Int(section["RetentionMinutes"], defaults.RetentionMinutes, minimum: 1, "Worker:RetentionMinutes", collected),
            StaleProcessingMinutes = Int(section["StaleProcessingMinutes"], defaults.StaleProcessingMinutes, minimum: 1, "Worker:StaleProcessingMinutes", collected),
        };

        warnings = collected;
        return options;
    }

    private static string Text(string? raw, string fallback) =>
        string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();

    private static string OneOf(string? raw, string[] allowed, string fallback, string key, List<string> warnings)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        var match = allowed.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            warnings.Add($"{key}: '{raw}' is not one of {string.Join(", ", allowed)}; using {fallback}.");
            return fallback;
        }

        return match;
    }

    private static int Int(string? raw, int fallback, int minimum, string key, List<string> warnings)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum)
        {
            warnings.Add($"{key}: '{raw}' is not a whole number of at least {minimum}; using {fallback}.");
            return fallback;
        }

        return parsed;
    }
}
