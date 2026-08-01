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
}
