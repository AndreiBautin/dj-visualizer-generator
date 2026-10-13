namespace DjVisualizer.Application.Jobs;

public sealed record JobStatusResult(string JobId, string Status, int Progress, string? ErrorMessage, int DownloadsRemaining, string Preset, string CaptionFont, double RotationSpeedSeconds, double LabelFocusX, double LabelFocusY, double LabelZoom);
