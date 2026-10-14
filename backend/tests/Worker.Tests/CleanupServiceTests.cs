using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Hosting.Configuration;
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

    private DjVisualizer.Worker.Hosting.CleanupService CreateSut() => new(
        _jobRepository,
        _clock,
        Options.Create(_options),
        NullLogger<DjVisualizer.Worker.Hosting.CleanupService>.Instance);

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

    /// <summary>On Windows a video still open for a download cannot be deleted; that one failure
    /// used to end the sweep, so every job after it was never cleaned up while the file stayed open.</summary>
    [Fact]
    public async Task RunSweepAsync_Keeps_Going_When_One_Job_Cannot_Be_Deleted()
    {
        var locked = CompletedJob(Now.AddHours(-2));
        var next = FailedJob(Now.AddHours(-2));
        GivenJobs(locked, next);
        _jobRepository.DeleteAsync(locked.Id, Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("in use")));

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.Received(1).DeleteAsync(next.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunSweepAsync_Removes_Folders_No_Job_Record_Describes_Once_Untouched_For_The_Stale_Threshold()
    {
        GivenJobs();

        await CreateSut().RunSweepAsync(CancellationToken.None);

        await _jobRepository.Received(1).DeleteUnreadableFoldersAsync(TimeSpan.FromMinutes(_options.StaleProcessingMinutes), Arg.Any<CancellationToken>());
    }
}
