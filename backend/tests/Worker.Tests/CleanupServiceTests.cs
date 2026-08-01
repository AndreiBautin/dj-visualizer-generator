using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DjVisualizer.Worker.Tests;

public class CleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WorkerOptions _options = new() { StaleProcessingMinutes = 60, RetentionMinutes = 60 };

    public CleanupServiceTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private DjVisualizer.Worker.CleanupService CreateSut() => new(
        _jobRepository,
        _clock,
        Options.Create(_options),
        NullLogger<DjVisualizer.Worker.CleanupService>.Instance);

    private static Job ProcessingJob(DateTimeOffset updatedAt)
    {
        var job = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, updatedAt);
        job.Start(updatedAt);
        return job;
    }

    private static Job CompletedJob(DateTimeOffset updatedAt)
    {
        var job = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, updatedAt);
        job.Start(updatedAt);
        job.Complete(updatedAt);
        return job;
    }

    private static Job FailedJob(DateTimeOffset updatedAt)
    {
        var job = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, updatedAt);
        job.Fail("boom", updatedAt);
        return job;
    }

    private void GivenJobs(params Job[] jobs) => _jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(ToAsyncEnumerable(jobs));

    private static async IAsyncEnumerable<Job> ToAsyncEnumerable(Job[] jobs)
    {
        foreach (var job in jobs)
        {
            yield return job;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task RunSweepAsync_Fails_A_Processing_Job_Stuck_Past_The_Stale_Threshold()
    {
        var staleJob = ProcessingJob(Now.AddMinutes(-90));
        GivenJobs(staleJob);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.Received(1).SaveAsync(
            Arg.Is<Job>(j => j!.Status == JobStatus.Failed),
            Arg.Any<CancellationToken>());
        await _jobRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task RunSweepAsync_Leaves_A_Recently_Updated_Processing_Job_Alone()
    {
        var recentJob = ProcessingJob(Now.AddMinutes(-5));
        GivenJobs(recentJob);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _jobRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task RunSweepAsync_Deletes_A_Completed_Job_Past_The_Retention_Window()
    {
        var oldCompleted = CompletedJob(Now.AddMinutes(-90));
        GivenJobs(oldCompleted);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.Received(1).DeleteAsync(oldCompleted.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunSweepAsync_Deletes_A_Failed_Job_Past_The_Retention_Window()
    {
        var oldFailed = FailedJob(Now.AddMinutes(-90));
        GivenJobs(oldFailed);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.Received(1).DeleteAsync(oldFailed.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunSweepAsync_Leaves_A_Recently_Completed_Job_Alone()
    {
        var recentCompleted = CompletedJob(Now.AddMinutes(-5));
        GivenJobs(recentCompleted);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task RunSweepAsync_Never_Touches_A_Queued_Job_Regardless_Of_Age()
    {
        var oldQueued = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now.AddDays(-1));
        GivenJobs(oldQueued);

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _jobRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
