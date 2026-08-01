namespace DjVisualizer.Application.Abstractions;

public interface IAudioProbe
{
    Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken);
}
