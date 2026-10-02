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

        return new JobTitle(trimmed);
    }

    public override string ToString() => Value;
}
