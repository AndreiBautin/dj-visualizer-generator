namespace DjVisualizer.Api.Contracts;

/// <summary>Bound automatically from multipart/form-data: ASP.NET Core infers [FromForm]
/// for complex types containing an IFormFile property.</summary>
public sealed class CreateJobFormRequest
{
    public string? Title { get; set; }

    public string? Preset { get; set; }

    public IFormFile? Audio { get; set; }

    public IFormFile? Artwork { get; set; }

    /// <summary>Optional; seconds for one full spin. Defaults to <see cref="DjVisualizer.Domain.Jobs.RotationSpeed.DefaultSecondsPerRotation"/> if omitted.</summary>
    public double? RotationSpeedSeconds { get; set; }

    /// <summary>Optional; one of "sans-bold", "serif-bold", "mono-bold". Defaults to <see cref="DjVisualizer.Domain.Jobs.CaptionFont.Default"/> if omitted.</summary>
    public string? CaptionFont { get; set; }

    /// <summary>Optional; 0..1, where the label's crop window sits horizontally (0 = left, 0.5 = centre). Defaults to 0.5.</summary>
    public double? LabelFocusX { get; set; }

    /// <summary>Optional; 0..1, where the label's crop window sits vertically (0 = top, 0.5 = centre). Defaults to 0.5.</summary>
    public double? LabelFocusY { get; set; }

    /// <summary>Optional; 1..4, how far the label zooms into the artwork (1 = the largest square that fits). Defaults to 1.</summary>
    public double? LabelZoom { get; set; }
}
