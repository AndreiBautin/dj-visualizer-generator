namespace DjVisualizer.Application.Jobs;

/// <summary>One API instance serializes download admission through persistence, not file transfer.</summary>
public sealed class JobDownloadGate : IDisposable
{
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
