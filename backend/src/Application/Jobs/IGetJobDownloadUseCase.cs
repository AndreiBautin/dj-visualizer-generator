using DjVisualizer.Application.Common;

namespace DjVisualizer.Application.Jobs;

public interface IGetJobDownloadUseCase
{
    /// <param name="bytesToServe">Given the file's length, how many bytes this response will send;
    /// that, not the whole file, is reserved against the egress budget.</param>
    Task<Result<JobDownloadResult>> ExecutePreviewAsync(string jobId, Func<long, long> bytesToServe, CancellationToken cancellationToken);

    Task<Result<JobDownloadResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken);
}
