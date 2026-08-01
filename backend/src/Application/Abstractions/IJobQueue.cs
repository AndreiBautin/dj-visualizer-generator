using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

/// <summary>The API-to-Worker handoff: producer side creates jobs, consumer side claims them.</summary>
public interface IJobQueue
{
    Task EnqueueAsync(Job job, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims the oldest Queued job by transitioning it to Processing and persisting
    /// that before returning it, so a concurrent poll can't pick up the same job twice. Returns
    /// null if no job is waiting. Assumes a single Worker instance for MVP, so this claim does not
    /// need cross-process locking beyond the store's own atomic write.
    /// </summary>
    Task<Job?> TryDequeueNextAsync(CancellationToken cancellationToken);
}
