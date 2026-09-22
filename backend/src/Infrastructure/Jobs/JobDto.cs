namespace DjVisualizer.Infrastructure.Jobs;

internal sealed record JobDto(
    string Id,
    string Title,
    string Preset,
    double RotationSpeedSecondsPerRotation,
    string CaptionFont,
    string Status,
    int Progress,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // Absent from status.json files written before download limiting existed. System.Text.Json
    // leaves it at 0, which is exactly right: a job nobody counted downloads for has had none
    // counted, so it starts with its full allowance rather than being locked out.
    int DownloadCount = 0);
