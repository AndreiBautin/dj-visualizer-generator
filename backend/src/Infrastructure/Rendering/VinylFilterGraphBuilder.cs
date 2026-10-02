using System.Globalization;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Builds the ffmpeg filter_complex graphs for the spinning-record visual, split into passes so
/// the expensive per-pixel <c>geq</c> math runs exactly once instead of on every output frame:
///
/// Pass 1 (<see cref="BuildStaticVinylGraph"/>): draws the record - a grooved black disc with a
/// soft highlight, the artwork as its centre label, and the spindle hole - as one static image.
///
/// Pass 2 (<see cref="BuildBackgroundGraph"/>): the backdrop, also a single static image - black
/// with a faint accent glow toward the top-left, the same as the app's own page.
///
/// Pass 3 (<see cref="BuildRotatingCompositeGraph"/>): loops the record image, applying only the
/// (much cheaper) continuous rotation, composites it over the backdrop (input 1), and overlays
/// the title bottom-center.
///
/// <para><b>The record is the homepage's record.</b> The video used to crop the artwork to a
/// full disc with a white border over a blurred full-frame copy of it. Asked for as "make the
/// video identical to the spinning preview on the homepage": every proportion here is taken from
/// <c>VinylRecord.tsx</c> - label at 40% of the record, spindle hole at 6%, grooves on a period of
/// four CSS pixels of a 22rem record, the highlight at 35%/30% - so the two cannot be told apart
/// except by size. Change one and change the other.</para>
/// </summary>
internal static class VinylFilterGraphBuilder
{
    private const double DiameterRatio = 0.74;
    private const double FontSizeRatio = 0.044;
    private const double BottomMarginRatio = 0.09;

    /// <summary>The homepage record is 22rem - 352 CSS pixels - and its gradients are written in
    /// pixels of that, so the render scales each by <c>diameter / 352</c>.</summary>
    private const double PreviewDiameterPx = 352;

    private const double LabelRatio = 0.40; // inset 30%
    private const double HoleRatio = 0.06; // inset 47%
    private const double HighlightInsetRatio = 0.88; // inset 6%

    // The page's accent, oklch(62% 0.19 235), is sRGB (0, 147, 230); its glow is 14% of that at
    // 20%/20%, fading out at 55% of the distance to the far corner.
    private const int GlowGreen = 21;
    private const int GlowBlue = 32;

    private readonly record struct Geometry(int Diameter, int FontSize, int BottomMargin);

    /// <summary>
    /// The backdrop: black with the accent glow the app's page has, rendered once as a still. It
    /// reads no input - it used to be the artwork blurred to fill the frame.
    /// </summary>
    public static string BuildBackgroundGraph(VideoPreset preset)
    {
        var w = preset.Width;
        var h = preset.Height;
        var reach = Format(Math.Sqrt((0.8 * w * 0.8 * w) + (0.8 * h * 0.8 * h)) * 0.55);
        var falloff = $"max(0,1-hypot(X-{Format(0.2 * w)},Y-{Format(0.2 * h)})/{reach})";

        return $"color=c=black:s={w}x{h},format=rgb24,geq=r='0':g='{GlowGreen}*{falloff}':b='{GlowBlue}*{falloff}'[background]";
    }

    public static string BuildStaticVinylGraph(VideoPreset preset)
    {
        var d = ComputeGeometry(preset).Diameter;
        var px = d / PreviewDiameterPx;
        var radius = Format(d / 2.0);
        var r = $"hypot(X-{radius},Y-{radius})";

        // repeating-radial-gradient(#0a0a0a 0, #0a0a0a 2px, #1c1c1c 3px, #0a0a0a 4px).
        var step = Format(px);
        var m = $"mod({r},4*{step})";
        var groove = $"if(lt({m},2*{step}),10,if(lt({m},3*{step}),10+18*({m}-2*{step})/{step},28-18*({m}-3*{step})/{step}))";

        // The highlight - white at 6%, at 35%/30% of the record inset by 6%, gone at 60% of the
        // distance to that box's far corner - plus the 1px white-at-6% edge.
        var inner = HighlightInsetRatio * d;
        var offset = (1 - HighlightInsetRatio) / 2 * d;
        var hx = Format(offset + (0.35 * inner));
        var hy = Format(offset + (0.30 * inner));
        var reach = Format(0.6 * Math.Sqrt((0.65 * 0.65) + (0.70 * 0.70)) * inner);
        var light = $"if(lte({r},{Format(inner / 2)}),max(0,1-hypot(X-{hx},Y-{hy})/{reach}),0)+if(gt({r},{radius}-{step}),1,0)";
        var shade = $"({groove})+(255-({groove}))*0.06*min(1,{light})";
        var discAlpha = $"clip(255*({radius}-{r}+0.5),0,255)";

        var label = (int)Math.Round(d * LabelRatio);
        var labelRadius = Format(label / 2.0);

        var hole = (int)Math.Round(d * HoleRatio);
        var ring = Math.Max(1, (int)Math.Round(2 * px));
        var holeCanvas = hole + (2 * ring);
        var holeCentre = Format(holeCanvas / 2.0);
        var hr = $"hypot(X-{holeCentre},Y-{holeCentre})";
        var inHole = $"lte({hr},{Format(hole / 2.0)})";

        var stages = new[]
        {
            $"color=c=black:s={d}x{d},format=rgba,geq=r='{shade}':g='{shade}':b='{shade}':a='{discAlpha}'[disc]",
            $"[0:v]scale={label}:{label}:force_original_aspect_ratio=increase,crop={label}:{label},format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='clip(255*({labelRadius}-hypot(X-{labelRadius},Y-{labelRadius})+0.5),0,255)'[label]",
            // The spindle hole, ringed in white at 15% as the homepage's box-shadow draws it.
            $"color=c=black:s={holeCanvas}x{holeCanvas},format=rgba,geq=r='if({inHole},0,255)':g='if({inHole},0,255)':b='if({inHole},0,255)':a='if({inHole},255,if(lte({hr},{holeCentre}),38,0))'[hole]",
            "[disc][label]overlay=(W-w)/2:(H-h)/2:format=auto[with_label]",
            "[with_label][hole]overlay=(W-w)/2:(H-h)/2:format=auto[vinyl_static]",
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
            // Positive angles turn clockwise, as the homepage's CSS spin does.
            $"[0:v]rotate={angularVelocity}*t:c=black@0.0:ow={g.Diameter}:oh={g.Diameter}[vinyl_rotating]",
            // Input 1 is the pre-rendered backdrop (see BuildBackgroundGraph) - already sized to
            // the full frame, so this is a cheap overlay, not a live generator. There is no drop
            // shadow: the homepage's is black on black and the record reads the same without one.
            "[1:v][vinyl_rotating]overlay=(W-w)/2:(H-h)/2:shortest=1[with_vinyl]",
            // expansion=none disables drawtext's %{...} text-expansion pass, so a title is drawn
            // literally instead of being interpreted (e.g. "%{gmtime}" stays as typed).
            $"[with_vinyl]drawtext=fontfile='{escapedFontFile}':textfile='{escapedTitleFile}':expansion=none:fontcolor=white:fontsize={g.FontSize}:x=(w-text_w)/2:y=h-{g.BottomMargin}:shadowcolor=black@0.5:shadowx=2:shadowy=2[final]",
        };

        return string.Join(";", stages);
    }

    private static Geometry ComputeGeometry(VideoPreset preset) => new(
        (int)Math.Round(preset.Height * DiameterRatio),
        (int)Math.Round(preset.Height * FontSizeRatio),
        (int)Math.Round(preset.Height * BottomMarginRatio));

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

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
