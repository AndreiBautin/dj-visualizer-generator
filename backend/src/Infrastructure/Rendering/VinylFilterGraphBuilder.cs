using System.Globalization;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Builds the ffmpeg filter_complex graphs for the spinning-record visual, split into passes so
/// the expensive per-pixel <c>geq</c>/blur math runs exactly once instead of on every output
/// frame:
///
/// Pass 1 (<see cref="BuildStaticVinylGraph"/>): crops the artwork to a circle with a white
/// border and renders it as a single static image.
///
/// Pass 2 (<see cref="BuildAmbientBackgroundGraph"/>): a separate static image - the same artwork
/// scaled to fill the frame, heavily blurred and darkened into an ambient glow, replacing a flat
/// black background.
///
/// Pass 3 (<see cref="BuildRotatingCompositeGraph"/>): loops the vinyl image, applying only the
/// (much cheaper) continuous rotation and a soft drop shadow, composites it over the ambient
/// background (input 1), and overlays the title bottom-center.
///
/// The geometry and the rotate filter's angle sign follow ffmpeg's documented behavior and have
/// been visually verified against real renders (still frames extracted and inspected showed the
/// expected circular crop, border, shadow, ambient background, and centering).
/// </summary>
internal static class VinylFilterGraphBuilder
{
    private const double DiameterRatio = 0.74;
    private const double BorderRatio = 0.016;
    private const int MinBorderWidth = 6;
    private const double FontSizeRatio = 0.044;
    private const double BottomMarginRatio = 0.09;

    // Shadow proportions are relative to the ring diameter so the "floating disc" effect looks
    // consistent across presets rather than a fixed pixel offset looking oversized on 720p or
    // negligible on 1080p.
    private const double ShadowOffsetXRatio = 0.026;
    private const double ShadowOffsetYRatio = 0.033;
    private const double ShadowBlurRadiusRatio = 0.018;
    private const int MinShadowBlurRadius = 4;

    private const int AmbientBlurSigma = 40;
    private const double AmbientBrightness = -0.15;
    private const double AmbientSaturation = 0.6;

    private readonly record struct Geometry(
        int Diameter,
        int RingDiameter,
        int FontSize,
        int BottomMargin,
        int ShadowOffsetX,
        int ShadowOffsetY,
        int ShadowBlurRadius);

    /// <summary>
    /// A soft, blurred, darkened full-frame version of the artwork itself, replacing a flat black
    /// background with an ambient glow of the artwork's own colors (in the style of Spotify
    /// Canvas / Apple Music's "now playing" background) - rendered once as a static image, just
    /// like <see cref="BuildStaticVinylGraph"/>, so it costs nothing per output frame.
    /// </summary>
    public static string BuildAmbientBackgroundGraph(VideoPreset preset)
    {
        var stages = new[]
        {
            $"[0:v]scale={preset.Width}:{preset.Height}:force_original_aspect_ratio=increase,crop={preset.Width}:{preset.Height}",
            $"gblur=sigma={AmbientBlurSigma}",
            $"eq=brightness={AmbientBrightness.ToString(CultureInfo.InvariantCulture)}:saturation={AmbientSaturation.ToString(CultureInfo.InvariantCulture)}[background]",
        };

        return string.Join(",", stages);
    }

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
    /// <param name="titleFilePath">Path to a UTF-8 file holding the caption text. The title is
    /// passed to drawtext by <c>textfile=</c> rather than inlined with <c>text=</c> because a
    /// title is caller-controlled and ffmpeg's filtergraph parser re-parses option values: inside
    /// a single-quoted option, ffmpeg does <em>not</em> honour <c>\'</c> as an escaped quote, so
    /// any title containing an apostrophe terminated the option early and injected the remainder
    /// into the graph. Reading the text from a file keeps caller-controlled bytes out of the
    /// graph string entirely, so there is no escaping rule left to get wrong.</param>
    public static string BuildRotatingCompositeGraph(VideoPreset preset, string titleFilePath, string fontFilePath, double rotationPeriodSeconds)
    {
        var g = ComputeGeometry(preset);
        var angularVelocity = (2 * Math.PI / rotationPeriodSeconds).ToString("G17", CultureInfo.InvariantCulture);
        var escapedTitleFile = EscapeFilterPath(titleFilePath);
        var escapedFontFile = EscapeFilterPath(fontFilePath);

        var stages = new[]
        {
            $"[0:v]rotate={angularVelocity}*t:c=black@0.0:ow={g.RingDiameter}:oh={g.RingDiameter}[vinyl_rotating]",
            // A soft shadow gives the disc a sense of depth instead of looking pasted flat onto
            // the background. `split` reuses the frame already rotated above instead of paying
            // for a second rotate pass; lutrgb forces a uniform dark-gray fill (a pure black
            // shadow would be invisible against the black background) and boxblur (a cheap
            // separable blur, unlike geq) softens its edge.
            "[vinyl_rotating]split=2[vinyl_main][vinyl_shadow_src]",
            $"[vinyl_shadow_src]format=rgba,lutrgb=r=30:g=30:b=30,colorchannelmixer=aa=0.55,boxblur=luma_radius={g.ShadowBlurRadius}:luma_power=2:chroma_radius={g.ShadowBlurRadius}:chroma_power=2:alpha_radius={g.ShadowBlurRadius}:alpha_power=2[vinyl_shadow]",
            // Input 1 is the pre-rendered ambient background (see BuildAmbientBackgroundGraph) -
            // already sized to the full frame, so this is a cheap overlay, not a live generator.
            $"[1:v][vinyl_shadow]overlay=(W-w)/2+{g.ShadowOffsetX}:(H-h)/2+{g.ShadowOffsetY}[with_shadow]",
            "[with_shadow][vinyl_main]overlay=(W-w)/2:(H-h)/2:shortest=1[with_vinyl]",
            // expansion=none disables drawtext's %{...} text-expansion pass, so a title is drawn
            // literally instead of being interpreted (e.g. "%{gmtime}" stays as typed).
            $"[with_vinyl]drawtext=fontfile='{escapedFontFile}':textfile='{escapedTitleFile}':expansion=none:fontcolor=white:fontsize={g.FontSize}:x=(w-text_w)/2:y=h-{g.BottomMargin}:shadowcolor=black@0.5:shadowx=2:shadowy=2[final]",
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
        var shadowOffsetX = (int)Math.Round(ringDiameter * ShadowOffsetXRatio);
        var shadowOffsetY = (int)Math.Round(ringDiameter * ShadowOffsetYRatio);
        var shadowBlurRadius = Math.Max(MinShadowBlurRadius, (int)Math.Round(ringDiameter * ShadowBlurRadiusRatio));
        return new Geometry(diameter, ringDiameter, fontSize, bottomMargin, shadowOffsetX, shadowOffsetY, shadowBlurRadius);
    }

    /// <summary>
    /// Escapes an <em>application-controlled</em> filesystem path for use inside a single-quoted
    /// ffmpeg filter option - specifically to survive Windows paths, whose drive colon and
    /// separators the filtergraph parser would otherwise treat as syntax. Order matters:
    /// backslashes are escaped first so the escaping added for the colon isn't itself re-escaped.
    /// </summary>
    /// <remarks>
    /// This is deliberately not used for caller-supplied text. ffmpeg does not honour <c>\'</c>
    /// inside a single-quoted option, so no amount of escaping makes an arbitrary string safe to
    /// inline; text reaches drawtext through <c>textfile=</c> instead. The paths passed here are
    /// built by the renderer (a temp file name it generated, or a configured font path), never
    /// from request data.
    /// </remarks>
    private static string EscapeFilterPath(string value) => value
        .Replace("\\", "\\\\")
        .Replace(":", "\\:")
        .Replace("'", "\\'")
        .Replace("%", "\\%");
}
