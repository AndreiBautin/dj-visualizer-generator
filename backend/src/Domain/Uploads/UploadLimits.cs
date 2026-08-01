using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Uploads;

public sealed class UploadLimits
{
    public long MaxAudioBytes { get; }
    public long MaxImageBytes { get; }
    public int MaxDurationSeconds { get; }
    public long MinFreeDiskBytes { get; }

    public UploadLimits(long maxAudioBytes, long maxImageBytes, int maxDurationSeconds, long minFreeDiskBytes)
    {
        if (maxAudioBytes <= 0)
        {
            throw new InvalidUploadLimitsException("Max audio bytes must be positive.");
        }

        if (maxImageBytes <= 0)
        {
            throw new InvalidUploadLimitsException("Max image bytes must be positive.");
        }

        if (maxDurationSeconds <= 0)
        {
            throw new InvalidUploadLimitsException("Max duration seconds must be positive.");
        }

        if (minFreeDiskBytes <= 0)
        {
            throw new InvalidUploadLimitsException("Min free disk bytes must be positive.");
        }

        MaxAudioBytes = maxAudioBytes;
        MaxImageBytes = maxImageBytes;
        MaxDurationSeconds = maxDurationSeconds;
        MinFreeDiskBytes = minFreeDiskBytes;
    }
}
