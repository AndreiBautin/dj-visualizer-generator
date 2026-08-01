using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Jobs;

public sealed class ProcessRenderJobUseCase(
    IJobInputFileLocator inputFileLocator,
    IAudioProbe audioProbe,
    IJobFileStorage fileStorage,
    IVideoRenderer videoRenderer,
    IJobRepository jobRepository,
    IClock clock) : IProcessRenderJobUseCase
{
    public async Task ExecuteAsync(Job job, CancellationToken cancellationToken)
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
            await FailAsync(job, ex.Message);
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
            job.CaptionFont);

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
            await FailAsync(job, ex.Message);
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
