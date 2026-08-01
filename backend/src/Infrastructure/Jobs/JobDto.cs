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
    DateTimeOffset UpdatedAt);
