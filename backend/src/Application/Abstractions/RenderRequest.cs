using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

public sealed record RenderRequest(
    string AudioFilePath,
    string ArtworkFilePath,
    string OutputFilePath,
    VideoPreset Preset,
    string Title,
    TimeSpan Duration,
    double RotationPeriodSeconds,
    CaptionFont CaptionFont);
