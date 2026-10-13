namespace DjVisualizer.Application.Jobs;

public sealed record CreateJobRequest(
    string Title,
    string PresetName,
    Stream AudioStream,
    string AudioFileName,
    Stream ArtworkStream,
    string ArtworkFileName,
    double? RotationSpeedSeconds = null,
    string? CaptionFontName = null,
    double? LabelFocusX = null,
    double? LabelFocusY = null,
    double? LabelZoom = null);
