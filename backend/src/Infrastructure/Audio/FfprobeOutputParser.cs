using System.Globalization;
using System.Text.Json;
using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Audio;

internal static class FfprobeOutputParser
{
    public static TimeSpan ParseDuration(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new AudioProbeException($"ffprobe returned output that could not be parsed as JSON: {ex.Message}");
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("format", out var format) ||
                !format.TryGetProperty("duration", out var durationProperty))
            {
                throw new AudioProbeException("ffprobe output did not include a duration.");
            }

            var durationText = durationProperty.GetString();
            if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                throw new AudioProbeException($"ffprobe reported a non-numeric duration: '{durationText}'.");
            }

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
