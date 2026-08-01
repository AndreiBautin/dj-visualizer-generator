using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class CaptionFont
{
    public static readonly CaptionFont SansBold = new("sans-bold");
    public static readonly CaptionFont SerifBold = new("serif-bold");
    public static readonly CaptionFont MonoBold = new("mono-bold");

    public string Name { get; }

    private CaptionFont(string name) => Name = name;

    public static CaptionFont Default => SansBold;

    public static CaptionFont FromName(string name) => name switch
    {
        "sans-bold" => SansBold,
        "serif-bold" => SerifBold,
        "mono-bold" => MonoBold,
        _ => throw new InvalidCaptionFontException(name),
    };

    public override string ToString() => Name;
}
