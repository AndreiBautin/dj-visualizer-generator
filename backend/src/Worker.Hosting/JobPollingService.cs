using System.Diagnostics;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DjVisualizer.Worker;

/// <summary>Polls for the next Queued job and processes it, one at a time - rendering is
/// CPU-bound and this is a single-Worker MVP, so there is no benefit to concurrent renders.</summary>
public sealed class JobPollingService(
    IJobQueue jobQueue,
    IProcessRenderJobUseCase processRenderJobUseCase,
    IOptions<WorkerOptions> options,
    ILogger<JobPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await PollOnceAsync(stoppingToken);

            if (!processed)
            {
                try
                {
                    await Task.Delay(pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task<bool> PollOnceAsync(CancellationToken cancellationToken)
    {
        Job? job;
        try
        {
            job = await jobQueue.TryDequeueNextAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to poll for queued jobs.");
            return false;
        }

        if (job is null)
        {
            return false;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = job.Id.ToString() });
        logger.LogInformation("Starting render for job {JobId}.", job.Id);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await processRenderJobUseCase.ExecuteAsync(job, cancellationToken);
            logger.LogInformation(
                "Finished job {JobId} with status {Status} in {ElapsedSeconds:F1}s.",
                job.Id,
                job.Status,
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ProcessRenderJobUseCase already handles expected failures internally (marks the job
            // Failed and persists it); reaching here means something unexpected slipped through.
            logger.LogError(ex, "Unhandled error processing job {JobId}.", job.Id);
        }

        return true;
    }
}
