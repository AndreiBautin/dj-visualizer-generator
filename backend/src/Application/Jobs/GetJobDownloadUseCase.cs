using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Jobs;

public sealed class GetJobDownloadUseCase(IJobRepository jobRepository, IJobFileStorage fileStorage) : IGetJobDownloadUseCase
{
    public async Task<Result<JobDownloadResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken)
    {
        JobId id;
        try
        {
            id = JobId.Parse(jobId);
        }
        catch (InvalidJobIdException)
        {
            return Result<JobDownloadResult>.Failure(Error.NotFound("No job exists for the given id."));
        }

        var job = await jobRepository.FindAsync(id, cancellationToken);
        if (job is null)
        {
            return Result<JobDownloadResult>.Failure(Error.NotFound("No job exists for the given id."));
        }

        if (job.Status != JobStatus.Completed)
        {
            return Result<JobDownloadResult>.Failure(new Error(ErrorCodes.NotReady, "The video is not ready to download yet."));
        }

        var filePath = await fileStorage.GetOutputFilePathAsync(job.Id, cancellationToken);
        if (filePath is null)
        {
            return Result<JobDownloadResult>.Failure(Error.Failure("The rendered video could not be found."));
        }

        return Result<JobDownloadResult>.Success(new JobDownloadResult(filePath, SanitizeFileName(job.Title.Value) + ".mp4"));
    }

    private static string SanitizeFileName(string title)
    {
        var sanitized = title;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized;
    }
}
