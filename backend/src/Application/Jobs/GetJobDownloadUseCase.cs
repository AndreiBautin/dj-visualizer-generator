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

    /// <summary>
    /// Characters replaced in a download filename. Deliberately a fixed set rather than
    /// <see cref="Path.GetInvalidFileNameChars"/>, which is <em>platform-dependent</em>: on
    /// Windows it returns roughly forty characters, on Linux only '/' and NUL. Using it would
    /// mean the same title produced a different filename depending on the server's OS - and since
    /// this app is developed on Windows and deployed in a Linux container, the deployed
    /// behaviour would be the untested one. It also sanitises for the wrong machine: the name
    /// travels in Content-Disposition and is written to disk by the <em>client</em>, whose OS the
    /// server cannot know, so the safe choice is the most restrictive common denominator.
    /// </summary>
    private static readonly char[] InvalidFileNameChars =
        [.. @"<>:""/\|?*".ToCharArray(), .. Enumerable.Range(0, 32).Select(c => (char)c)];

    private static string SanitizeFileName(string title)
    {
        var sanitized = title;
        foreach (var invalidChar in InvalidFileNameChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized;
    }
}
