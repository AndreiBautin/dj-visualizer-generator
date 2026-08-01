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
}
