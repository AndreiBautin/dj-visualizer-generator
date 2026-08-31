using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static Job CreateJob() =>
        Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);

    [Fact]
    public void Create_Starts_In_Queued_Status_With_Zero_Progress()
    {
        var job = CreateJob();

        job.Status.Should().Be(JobStatus.Queued);
        job.Progress.Should().Be(0);
        job.ErrorMessage.Should().BeNull();
        job.CreatedAt.Should().Be(Now);
        job.UpdatedAt.Should().Be(Now);
        job.RotationSpeed.Should().Be(RotationSpeed.Default);
        job.CaptionFont.Should().Be(CaptionFont.Default);
    }

    [Fact]
    public void Start_Transitions_Queued_To_Processing()
    {
        var job = CreateJob();
        var startedAt = Now.AddSeconds(5);

        job.Start(startedAt);

        job.Status.Should().Be(JobStatus.Processing);
        job.UpdatedAt.Should().Be(startedAt);
    }

    [Fact]
    public void Start_Throws_When_Job_Is_Not_Queued()
    {
        var job = CreateJob();
        job.Start(Now);

        var act = () => job.Start(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    [Fact]
    public void UpdateProgress_Sets_Progress_While_Processing()
    {
        var job = CreateJob();
        job.Start(Now);

        job.UpdateProgress(42, Now.AddMinutes(1));

        job.Progress.Should().Be(42);
        job.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void UpdateProgress_Throws_When_Job_Is_Not_Processing()
    {
        var job = CreateJob();

        var act = () => job.UpdateProgress(50, Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateProgress_Rejects_Values_Outside_0_To_100(int progress)
    {
        var job = CreateJob();
        job.Start(Now);

        var act = () => job.UpdateProgress(progress, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Complete_Transitions_Processing_To_Completed_And_Sets_Progress_To_100()
    {
        var job = CreateJob();
        job.Start(Now);
        job.UpdateProgress(80, Now);

        job.Complete(Now.AddMinutes(2));

        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.UpdatedAt.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void Complete_Throws_When_Job_Is_Not_Processing()
    {
        var job = CreateJob();

        var act = () => job.Complete(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    [Theory]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Processing)]
    public void Fail_Transitions_Queued_Or_Processing_To_Failed_With_Reason(JobStatus fromStatus)
    {
        var job = CreateJob();
        if (fromStatus == JobStatus.Processing)
        {
            job.Start(Now);
        }

        job.Fail("ffmpeg exited with code 1", Now.AddMinutes(3));

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("ffmpeg exited with code 1");
        job.UpdatedAt.Should().Be(Now.AddMinutes(3));
    }

    [Fact]
    public void Fail_Throws_When_Job_Is_Already_Terminal()
    {
        var job = CreateJob();
        job.Start(Now);
        job.Complete(Now);

        var act = () => job.Fail("too late", Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    private static Job CompletedJob()
    {
        var job = CreateJob();
        job.Start(Now);
        job.Complete(Now);
        return job;
    }

    [Fact]
    public void RecordDownload_Counts_Downloads_Until_The_Limit_Is_Reached()
    {
        var job = CompletedJob();

        for (var i = 0; i < Job.MaxDownloads; i++)
        {
            job.DownloadLimitReached.Should().BeFalse($"only {i} of {Job.MaxDownloads} downloads have been recorded");
            job.RecordDownload();
        }

        job.DownloadCount.Should().Be(Job.MaxDownloads);
        job.DownloadLimitReached.Should().BeTrue();

        var act = job.RecordDownload;

        act.Should().Throw<DownloadLimitExceededException>();
    }

    /// <summary>
    /// The trap this guards. Retention sweeps delete terminal jobs once <c>UpdatedAt</c> is older
    /// than the retention window, so if a download touched that timestamp anyone holding the id
    /// could keep the job - and its video - alive forever by re-downloading inside the window.
    /// The download limit would then be the only thing left bounding storage as well as egress,
    /// and a job that should have been swept would linger instead.
    /// </summary>
    [Fact]
    public void RecordDownload_Does_Not_Extend_The_Jobs_Retention_Window()
    {
        var job = CompletedJob();
        var completedAt = job.UpdatedAt;

        job.RecordDownload();

        job.UpdatedAt.Should().Be(completedAt);
    }

    [Theory]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Failed)]
    public void RecordDownload_Throws_When_The_Job_Has_No_Video_To_Download(JobStatus status)
    {
        var job = CreateJob();
        if (status is JobStatus.Processing or JobStatus.Failed)
        {
            job.Start(Now);
        }

        if (status == JobStatus.Failed)
        {
            job.Fail("ffmpeg exited with code 1", Now);
        }

        var act = job.RecordDownload;

        act.Should().Throw<InvalidJobStateTransitionException>();
        job.DownloadCount.Should().Be(0);
    }

    /// <summary>
    /// The count is persisted in status.json and read back on every download, so a rehydrated job
    /// that forgot it would hand a used-up id its full allowance again on the next request - and
    /// on every request after that, since each one rehydrates afresh.
    /// </summary>
    [Fact]
    public void Rehydrate_Restores_The_Download_Count()
    {
        var job = Job.Rehydrate(
            JobId.New(),
            JobTitle.Create("Friday Night Set"),
            VideoPreset.FullHd1080p,
            RotationSpeed.Default,
            CaptionFont.Default,
            JobStatus.Completed,
            progress: 100,
            errorMessage: null,
            createdAt: Now,
            updatedAt: Now,
            downloadCount: Job.MaxDownloads);

        job.DownloadCount.Should().Be(Job.MaxDownloads);
        job.DownloadLimitReached.Should().BeTrue();
    }
}
