namespace DjVisualizer.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
