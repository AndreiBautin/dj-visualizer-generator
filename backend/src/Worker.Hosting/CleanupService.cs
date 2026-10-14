using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            // One job failing - on Windows, a video still open for a download cannot be deleted -
            // must not stop the sweep for every job after it.
            try
            {
                await SweepJobAsync(job, now, staleThreshold, retention, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Cleanup skipped job {JobId} this sweep.", job.Id);
            }
        }

        // Folders no job record describes. The stale threshold doubles as "long enough that no
        // upload is still writing here".
        var orphans = await jobRepository.DeleteUnreadableFoldersAsync(staleThreshold, cancellationToken);
        if (orphans > 0)
        {
            logger.LogInformation("Deleted {Count} job folders with no readable status.", orphans);
        }
    }

    private async Task SweepJobAsync(Job job, DateTimeOffset now, TimeSpan staleThreshold, TimeSpan retention, CancellationToken cancellationToken)
    {
        if (job.IsStale(now, staleThreshold))
        {
            job.Fail(Job.StaleFailureMessage, now);
            await jobRepository.SaveAsync(job, cancellationToken);
            logger.LogWarning(
                "Marked stale job {JobId} as Failed after {Minutes} minutes without progress.",
                job.Id,
                staleThreshold.TotalMinutes);
            return;
        }

        if (job.IsPastRetention(now, retention))
        {
            await jobRepository.DeleteAsync(job.Id, cancellationToken);
            logger.LogInformation("Deleted {Status} job {JobId} after its retention period.", job.Status, job.Id);
        }
    }
}
