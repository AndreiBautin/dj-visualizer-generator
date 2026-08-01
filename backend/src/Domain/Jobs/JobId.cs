using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public readonly record struct JobId
{
    private readonly Guid _value;

    private JobId(Guid value) => _value = value;

    public static JobId New() => new(Guid.NewGuid());

    public static JobId Parse(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var guid))
        {
            throw new InvalidJobIdException(value);
        }

        return new JobId(guid);
    }

    public override string ToString() => _value.ToString("D");
}
