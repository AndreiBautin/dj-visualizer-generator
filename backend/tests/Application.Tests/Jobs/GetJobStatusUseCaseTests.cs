using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;
using NSubstitute;

namespace DjVisualizer.Application.Tests.Jobs;

public class GetJobStatusUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();

    private GetJobStatusUseCase CreateSut() => new(_jobRepository);

    [Fact]
    public async Task ExecuteAsync_Returns_NotFound_For_A_Malformed_Job_Id()
    {
        var result = await CreateSut().ExecuteAsync("not-a-guid", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_NotFound_When_No_Job_Exists_For_The_Id()
    {
        var jobId = JobId.New();
        _jobRepository.FindAsync(jobId, Arg.Any<CancellationToken>()).Returns((Job?)null);

        var result = await CreateSut().ExecuteAsync(jobId.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Maps_A_Found_Job_To_Its_Status_Snapshot()
    {
        var job = Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.Hd720p, RotationSpeed.Default, CaptionFont.Default, Now);
        job.Start(Now);
        job.UpdateProgress(55, Now);
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.JobId.Should().Be(job.Id.ToString());
        result.Value.Status.Should().Be("Processing");
        result.Value.Progress.Should().Be(55);
        result.Value.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Includes_The_Error_Message_For_A_Failed_Job()
    {
        var job = Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.Hd720p, RotationSpeed.Default, CaptionFont.Default, Now);
        job.Fail("ffmpeg exited with code 1", Now);
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Failed");
        result.Value.ErrorMessage.Should().Be("ffmpeg exited with code 1");
    }
}
