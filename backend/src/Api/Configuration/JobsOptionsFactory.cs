using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace DjVisualizer.Api.Configuration;

/// <summary>
/// Builds <see cref="JobsOptions"/> from raw configuration strings, tolerating malformed values.
/// </summary>
/// <remarks>
/// This exists instead of a plain <c>Configuration.Get&lt;JobsOptions&gt;()</c> because every one
/// of these values arrives as an environment variable on a deployed instance, and the binder
/// throws on the first unparseable one. A container that exits during host construction gives an
/// operator a crash loop and a stack trace; degrading to the documented default and reporting a
/// warning gives them a running app and a log line naming the variable. Parsing is pure and
/// total: it never throws, and every rejected value falls back rather than being read as zero.
/// </remarks>
public static class JobsOptionsFactory
{
    public static JobsOptions Create(IConfiguration configuration, out IReadOnlyList<string> warnings)
    {
        var section = configuration.GetSection(JobsOptions.SectionName);
        var collected = new List<string>();
        var defaults = new JobsOptions();

        var options = new JobsOptions
        {
            RootPath = section["RootPath"] ?? defaults.RootPath,
            SingleContainer = ParseFlag(section["SingleContainer"], defaults.SingleContainer, "Jobs:SingleContainer", collected),
            MaxAudioBytes = ParsePositiveLong(section["MaxAudioBytes"], defaults.MaxAudioBytes, "Jobs:MaxAudioBytes", collected),
            MaxImageBytes = ParsePositiveLong(section["MaxImageBytes"], defaults.MaxImageBytes, "Jobs:MaxImageBytes", collected),
            MaxDurationSeconds = ParsePositiveInt(section["MaxDurationSeconds"], defaults.MaxDurationSeconds, "Jobs:MaxDurationSeconds", collected),
            MinFreeDiskBytes = ParsePositiveLong(section["MinFreeDiskBytes"], defaults.MinFreeDiskBytes, "Jobs:MinFreeDiskBytes", collected),
        };

        warnings = collected;
        return options;
    }

    /// <summary>
    /// Accepts the spellings people actually type into a hosting dashboard, not just the two the
    /// .NET binder accepts. Anything else falls back - a typo must never be read as the opposite
    /// mode, which for this flag would mean silently starting without a render worker.
    /// </summary>
    internal static bool ParseFlag(string? raw, bool fallback, string key, List<string> warnings)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        switch (value.ToLowerInvariant())
        {
            case "true" or "1" or "yes" or "on":
                return true;
            case "false" or "0" or "no" or "off":
                return false;
            default:
                warnings.Add($"{key}: '{raw}' is not a recognised true/false value; using {fallback}.");
                return fallback;
        }
    }

    internal static long ParsePositiveLong(string? raw, long fallback, string key, List<string> warnings)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        // Non-positive is rejected as well as unparseable: UploadLimits throws on a non-positive
        // limit, so accepting 0 here would just move the crash a few lines later.
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            warnings.Add($"{key}: '{raw}' is not a positive whole number; using {fallback}.");
            return fallback;
        }

        return parsed;
    }

    internal static int ParsePositiveInt(string? raw, int fallback, string key, List<string> warnings)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            warnings.Add($"{key}: '{raw}' is not a positive whole number; using {fallback}.");
            return fallback;
        }

        return parsed;
    }
}
