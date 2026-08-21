namespace DjVisualizer.Api.Contracts;

/// <summary>Render settings for a job built from the bundled sample assets. Every field is
/// optional; omitted fields fall back to the same defaults a normal upload would use.</summary>
public sealed class CreateSampleJobRequest
{
    public string? Title { get; set; }

    public string? Preset { get; set; }

    public double? RotationSpeedSeconds { get; set; }

    public string? CaptionFont { get; set; }
}
