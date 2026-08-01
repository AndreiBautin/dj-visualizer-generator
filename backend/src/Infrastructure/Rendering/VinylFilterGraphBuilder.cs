using System.Globalization;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Builds the ffmpeg filter_complex graphs for the spinning-record visual, split into two passes
/// so the expensive per-pixel <c>geq</c> circular-mask/border math runs exactly once instead of on
/// every output frame:
///
/// Pass 1 (<see cref="BuildStaticVinylGraph"/>): crops the artwork to a circle with a white
/// border and renders it as a single static image.
///
/// Pass 2 (<see cref="BuildRotatingCompositeGraph"/>): loops that static image, applying only the
/// (much cheaper) continuous rotation, composites it centered on a black background, and overlays
/// the title bottom-center.
///
/// The geometry and the rotate filter's angle sign follow ffmpeg's documented behavior and have
/// been visually verified against a real render (a still frame extracted and inspected showed the
/// expected circular crop, border, and centering).
/// </summary>
internal static class VinylFilterGraphBuilder
{
    private const double DiameterRatio = 0.74;
    private const double BorderRatio = 0.01;
    private const int MinBorderWidth = 4;
    private const double FontSizeRatio = 0.044;
    private const double BottomMarginRatio = 0.09;

    private readonly record struct Geometry(int Diameter, int RingDiameter, int FontSize, int BottomMargin);

    public static string BuildStaticVinylGraph(VideoPreset preset)
    {
        var g = ComputeGeometry(preset);

        var stages = new[]
        {
            $"[0:v]scale={g.Diameter}:{g.Diameter}:force_original_aspect_ratio=increase,crop={g.Diameter}:{g.Diameter},format=rgba[artwork_sq]",
            $"[artwork_sq]geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(lte(hypot(X-{g.Diameter}/2,Y-{g.Diameter}/2),{g.Diameter}/2),255,0)'[artwork_circle]",
            $"color=c=white:s={g.RingDiameter}x{g.RingDiameter},format=rgba,geq=r=255:g=255:b=255:a='if(between(hypot(X-{g.RingDiameter}/2,Y-{g.RingDiameter}/2),{g.Diameter}/2,{g.RingDiameter}/2),255,0)'[ring]",
            "[ring][artwork_circle]overlay=(W-w)/2:(H-h)/2:format=auto[vinyl_static]",
        };

        return string.Join(";", stages);
    }

    /// <param name="rotationPeriodSeconds">Seconds for one full rotation. Callers are responsible
    /// for passing a value already snapped to a whole number of output frames (see
    /// <see cref="FfmpegArgumentsBuilder.SnapRotationPeriodToFrames"/>) - otherwise the looped
    /// render will have a visible jump where it wraps back to frame 0.</param>
    public static string BuildRotatingCompositeGraph(VideoPreset preset, string title, string fontFilePath, double rotationPeriodSeconds)
    {
        var g = ComputeGeometry(preset);
        var angularVelocity = (2 * Math.PI / rotationPeriodSeconds).ToString("G17", CultureInfo.InvariantCulture);
        var escapedTitle = EscapeDrawTextValue(title);
        var escapedFontFile = EscapeDrawTextValue(fontFilePath);

        var stages = new[]
        {
            $"[0:v]rotate={angularVelocity}*t:c=black@0.0:ow={g.RingDiameter}:oh={g.RingDiameter}[vinyl_rotating]",
            $"color=c=black:s={preset.Width}x{preset.Height}[bg]",
            "[bg][vinyl_rotating]overlay=(W-w)/2:(H-h)/2:shortest=1[with_vinyl]",
            $"[with_vinyl]drawtext=fontfile='{escapedFontFile}':text='{escapedTitle}':fontcolor=white:fontsize={g.FontSize}:x=(w-text_w)/2:y=h-{g.BottomMargin}:shadowcolor=black@0.5:shadowx=2:shadowy=2[final]",
        };

        return string.Join(";", stages);
    }

    private static Geometry ComputeGeometry(VideoPreset preset)
    {
        var diameter = (int)Math.Round(preset.Height * DiameterRatio);
        var borderWidth = Math.Max(MinBorderWidth, (int)Math.Round(diameter * BorderRatio));
        var ringDiameter = diameter + (2 * borderWidth);
        var fontSize = (int)Math.Round(preset.Height * FontSizeRatio);
        var bottomMargin = (int)Math.Round(preset.Height * BottomMarginRatio);
        return new Geometry(diameter, ringDiameter, fontSize, bottomMargin);
    }

    /// <summary>Escapes a value for use inside a single-quoted ffmpeg filter option, per
    /// ffmpeg's filtergraph + drawtext double-escaping rules. Order matters: backslashes must be
    /// escaped first so the escaping added for the other characters isn't itself re-escaped.</summary>
    private static string EscapeDrawTextValue(string value) => value
        .Replace("\\", "\\\\")
        .Replace(":", "\\:")
        .Replace("'", "\\'")
        .Replace("%", "\\%");
}
