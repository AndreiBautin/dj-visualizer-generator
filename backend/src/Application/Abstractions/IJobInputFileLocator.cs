using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

/// <summary>Resolves a previously-saved job's input file paths on disk, without the caller
/// needing to know which extension IJobFileStorage picked for each file.</summary>
public interface IJobInputFileLocator
{
    Task<JobInputFiles?> LocateAsync(JobId jobId, CancellationToken cancellationToken);
}
