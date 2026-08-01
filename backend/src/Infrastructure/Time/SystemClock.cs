using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
