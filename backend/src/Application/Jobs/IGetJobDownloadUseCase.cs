using DjVisualizer.Application.Common;

namespace DjVisualizer.Application.Jobs;

public interface IGetJobDownloadUseCase
{
    Task<Result<JobDownloadResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken);
}
