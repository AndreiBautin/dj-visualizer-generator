using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using Microsoft.Extensions.Logging;

namespace DjVisualizer.Application.Jobs;

public sealed class ProcessRenderJobUseCase(
    IJobInputFileLocator inputFileLocator,
    IAudioProbe audioProbe,
    IJobFileStorage fileStorage,
    IVideoRenderer videoRenderer,
    IJobRepository jobRepository,
    IClock clock,
    ILogger<ProcessRenderJobUseCase> logger) : IProcessRenderJobUseCase
{
    // A job's ErrorMessage is served verbatim to whoever polls GET /jobs/{id}, so it must stay
    // free of diagnostic detail: ffmpeg and ffprobe report failures with the full command line,
    // which embeds absolute server paths and the internal filter graph. The underlying exception
    // is logged instead, where operators can see it and end users cannot.
    // The same words the upload path uses, so a visitor sees one message for one problem.
    public const string AudioUnreadableMessage = CreateJobUseCase.AudioUnreadableMessage;

    public const string RenderFailedMessage =
        "Rendering failed. The uploaded audio or artwork may be corrupt or in an unsupported format.";

    public async Task ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        try
        {
            await RenderAsync(job, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Anything not already turned into a failure above - a disk error preparing the output,
            // a failed progress save, a domain rule tripped mid-render. Escaping here left the job
            // Processing behind a frozen bar until the stale sweep, an hour later by default.
            if (job.Status == JobStatus.Processing)
            {
                logger.LogError(ex, "Processing failed unexpectedly for job {JobId}.", job.Id);
                await FailAsync(job, RenderFailedMessage);
                return;
            }

            throw;
        }
    }

    private async Task RenderAsync(Job job, CancellationToken cancellationToken)
    {
        var inputFiles = await inputFileLocator.LocateAsync(job.Id, cancellationToken);
        if (inputFiles is null)
        {
            await FailAsync(job, "Job input files were not found.");
            return;
        }

        TimeSpan duration;
        try
        {
            duration = await audioProbe.GetDurationAsync(inputFiles.AudioFilePath, cancellationToken);
        }
        catch (AudioProbeException ex)
        {
            logger.LogError(ex, "Probing audio failed for job {JobId}.", job.Id);
            await FailAsync(job, AudioUnreadableMessage);
            return;
        }

        var outputPath = await fileStorage.PrepareOutputFilePathAsync(job.Id, cancellationToken);
        var request = new RenderRequest(
            inputFiles.AudioFilePath,
            inputFiles.ArtworkFilePath,
            outputPath,
            job.Preset,
            job.Title.Value,
            duration,
            job.RotationSpeed.SecondsPerRotation,
            job.CaptionFont,
            job.LabelCrop);

        try
        {
            await videoRenderer.RenderAsync(
                request,
                async (percent, progressCancellationToken) =>
                {
                    job.UpdateProgress(percent, clock.UtcNow);
                    await jobRepository.SaveAsync(job, progressCancellationToken);
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown mid-render: leave the job Processing rather than mark it Failed.
            // Stale-job reconciliation on the next Worker startup will fail it if it's truly abandoned.
            throw;
        }
        catch (RenderException ex)
        {
            logger.LogError(ex, "Rendering failed for job {JobId}.", job.Id);
            await FailAsync(job, RenderFailedMessage);
            return;
        }

        job.Complete(clock.UtcNow);
        await jobRepository.SaveAsync(job, CancellationToken.None);
    }

    private async Task FailAsync(Job job, string reason)
    {
        job.Fail(reason, clock.UtcNow);
        await jobRepository.SaveAsync(job, CancellationToken.None);
    }
}
