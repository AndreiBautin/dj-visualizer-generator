namespace DjVisualizer.Api.Contracts;

/// <summary>The effective upload limits for this instance, so the UI can state them accurately
/// rather than hardcoding the self-hosted defaults.</summary>
public sealed record UploadLimitsResponse(long MaxAudioBytes, long MaxImageBytes, int MaxDurationSeconds);
