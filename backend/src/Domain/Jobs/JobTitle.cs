using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class JobTitle
{
    public const int MaxLength = 200;

    /// <summary>The caption, or empty when the video has none.</summary>
    public string Value { get; }

    /// <summary>Whether there is a caption to draw. A title is optional - asked for as "can we make
    /// title optional" - and a blank one means the record spins with nothing under it.</summary>
    public bool HasText => Value.Length > 0;

    public static JobTitle None { get; } = new(string.Empty);

    private JobTitle(string value) => Value = value;

    public static JobTitle Create(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return None;
        }

        if (trimmed.Length > MaxLength)
        {
            throw new InvalidJobTitleException($"Title must not exceed {MaxLength} characters.");
        }

        if (trimmed.Any(IsDisallowed))
        {
            throw new InvalidJobTitleException("Title must be a single line of visible text, without control or text-direction characters.");
        }

        return new JobTitle(trimmed);
    }

    /// <summary>
    /// The title is drawn into the video and becomes the download's file name. Control characters
    /// and line breaks have no place in either, and the bidirectional overrides can make a name
    /// display reversed - "mix\u202Ekcod.mp4" shows as "mixdocx.mp4"-style spoofing of the
    /// extension. Other format characters (the zero-width joiners in emoji sequences) are kept.
    /// </summary>
    private static bool IsDisallowed(char c) =>
        char.IsControl(c)
        || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator
        || c is '\u061C' or '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    public override string ToString() => Value;
}
