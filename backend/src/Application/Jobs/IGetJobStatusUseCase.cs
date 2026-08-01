using DjVisualizer.Application.Common;

namespace DjVisualizer.Application.Jobs;

public interface IGetJobStatusUseCase
{
    Task<Result<JobStatusResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken);
}
