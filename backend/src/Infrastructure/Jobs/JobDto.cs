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
    int DownloadCount = 0,
    // Absent from files written before label cropping existed; a null on any of them means the
    // job predates the feature and keeps the centre crop it was always rendered with.
    double? LabelFocusX = null,
    double? LabelFocusY = null,
    double? LabelZoom = null);
