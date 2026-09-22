using DjVisualizer.Application.Common;

namespace DjVisualizer.Application.Jobs;

public interface IGetJobDownloadUseCase
{
    Task<Result<JobDownloadResult>> ExecutePreviewAsync(string jobId, CancellationToken cancellationToken);

    Task<Result<JobDownloadResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken);
}
