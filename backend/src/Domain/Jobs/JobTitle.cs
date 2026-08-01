using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class JobTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    private JobTitle(string value) => Value = value;

    public static JobTitle Create(string value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidJobTitleException("Title must not be empty.");
        }

        if (trimmed.Length > MaxLength)
        {
            throw new InvalidJobTitleException($"Title must not exceed {MaxLength} characters.");
        }

        return new JobTitle(trimmed);
    }

    public override string ToString() => Value;
}
