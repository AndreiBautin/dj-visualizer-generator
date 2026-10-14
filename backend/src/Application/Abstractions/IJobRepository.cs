using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

/// <summary>Lookup, persistence, enumeration, and deletion of job state, keyed by id.</summary>
public interface IJobRepository
{
    Task<Job?> FindAsync(JobId id, CancellationToken cancellationToken);

    Task SaveAsync(Job job, CancellationToken cancellationToken);

    IAsyncEnumerable<Job> GetAllAsync(CancellationToken cancellationToken);

    Task DeleteAsync(JobId id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes job folders with no readable job record that nothing has written to for
    /// <paramref name="untouchedFor"/> - the leftovers of a crash mid-upload or a corrupt status
    /// file, which <see cref="GetAllAsync"/> skips and so nothing else would ever remove. Returns
    /// how many were deleted.
    /// </summary>
    Task<int> DeleteUnreadableFoldersAsync(TimeSpan untouchedFor, CancellationToken cancellationToken);
}
