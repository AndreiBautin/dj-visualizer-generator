using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

/// <summary>
/// Which part of the artwork becomes the circular record label. <see cref="FocusX"/> and
/// <see cref="FocusY"/> place the square crop window within the slack the artwork leaves
/// (0 = left/top, 0.5 = centre, 1 = right/bottom); <see cref="Zoom"/> shrinks that window
/// (1 = the largest square that fits, 2 = half its side).
/// </summary>
public sealed record LabelCrop
{
    public const double MinFocus = 0.0;
    public const double MaxFocus = 1.0;
    public const double MinZoom = 1.0;
    public const double MaxZoom = 4.0;

    public static readonly LabelCrop Default = new(0.5, 0.5, 1.0);

    public double FocusX { get; }
    public double FocusY { get; }
    public double Zoom { get; }

    private LabelCrop(double focusX, double focusY, double zoom)
    {
        FocusX = focusX;
        FocusY = focusY;
        Zoom = zoom;
    }

    public static LabelCrop Create(double focusX, double focusY, double zoom)
    {
        if (!double.IsFinite(focusX) || focusX is < MinFocus or > MaxFocus
            || !double.IsFinite(focusY) || focusY is < MinFocus or > MaxFocus
            || !double.IsFinite(zoom) || zoom is < MinZoom or > MaxZoom)
        {
            throw new InvalidLabelCropException(focusX, focusY, zoom);
        }

        return new LabelCrop(focusX, focusY, zoom);
    }
}
