using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace DjVisualizer.Worker;

/// <summary>
/// Periodically fails jobs stuck in Processing past a stale threshold (recovers from a crashed
/// Worker run) and deletes Completed/Failed job folders past their retention window, keeping
/// disk usage bounded since there is no database tracking storage separately.
/// </summary>
public sealed class CleanupService(
    IJobRepository jobRepository,
    IClock clock,
    IOptions<WorkerOptions> options,
    ILogger<CleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.CleanupIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cleanup sweep failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task RunSweepAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var staleThreshold = TimeSpan.FromMinutes(options.Value.StaleProcessingMinutes);
        var retention = TimeSpan.FromMinutes(options.Value.RetentionMinutes);

        await foreach (var job in jobRepository.GetAllAsync(cancellationToken))
        {
            if (job.Status == JobStatus.Processing && now - job.UpdatedAt > staleThreshold)
            {
                job.Fail("Worker stopped responding while processing this job.", now);
                await jobRepository.SaveAsync(job, cancellationToken);
                logger.LogWarning(
                    "Marked stale job {JobId} as Failed after {Minutes} minutes without progress.",
                    job.Id,
                    staleThreshold.TotalMinutes);
                continue;
            }

            var isTerminal = job.Status is JobStatus.Completed or JobStatus.Failed;
            if (isTerminal && now - job.UpdatedAt > retention)
            {
                await jobRepository.DeleteAsync(job.Id, cancellationToken);
                logger.LogInformation("Deleted {Status} job {JobId} after its retention period.", job.Status, job.Id);
            }
        }
    }
}
