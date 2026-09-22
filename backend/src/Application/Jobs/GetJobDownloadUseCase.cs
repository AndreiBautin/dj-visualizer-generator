using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Jobs;

public sealed class GetJobDownloadUseCase(
    IJobRepository jobRepository,
    IJobFileStorage fileStorage,
    IEgressBudget egressBudget,
    JobDownloadGate gate) : IGetJobDownloadUseCase
{
    public async Task<Result<JobDownloadResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken)
    {
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ResolveAsync(jobId, true, cancellationToken);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    public Task<Result<JobDownloadResult>> ExecutePreviewAsync(string jobId, CancellationToken cancellationToken) =>
        ResolveAsync(jobId, false, cancellationToken);

    private async Task<Result<JobDownloadResult>> ResolveAsync(string jobId, bool recordDownload, CancellationToken cancellationToken)
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

        if (recordDownload && job.DownloadLimitReached)
        {
            return Result<JobDownloadResult>.Failure(new Error(
                ErrorCodes.Exhausted,
                $"This video has already been downloaded {Job.MaxDownloads} times. Render it again to get a new link."));
        }

        var video = await fileStorage.GetRenderedVideoAsync(job.Id, cancellationToken);
        if (video is null)
        {
            return Result<JobDownloadResult>.Failure(Error.Failure("The rendered video could not be found."));
        }

        // Charged before the file is handed to the caller, and only once the job is known to be
        // downloadable - so a 404 or a not-ready poll costs nothing against the budget, and a
        // response that is about to be streamed cannot overshoot it.
        if (!egressBudget.TryReserve(video.SizeBytes))
        {
            return Result<JobDownloadResult>.Failure(new Error(
                ErrorCodes.Unavailable,
                "This instance has reached its download limit for now. Please try again later."));
        }

        if (recordDownload)
        {
            job.RecordDownload();
            await jobRepository.SaveAsync(job, cancellationToken);
        }

        return Result<JobDownloadResult>.Success(
            new JobDownloadResult(video.FilePath, SanitizeFileName(job.Title.Value) + ".mp4"));
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
