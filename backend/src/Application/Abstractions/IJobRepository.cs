using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

/// <summary>Lookup, persistence, enumeration, and deletion of job state, keyed by id.</summary>
public interface IJobRepository
{
    Task<Job?> FindAsync(JobId id, CancellationToken cancellationToken);

    Task SaveAsync(Job job, CancellationToken cancellationToken);

    IAsyncEnumerable<Job> GetAllAsync(CancellationToken cancellationToken);

    Task DeleteAsync(JobId id, CancellationToken cancellationToken);
}
