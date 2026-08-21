using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;
using NSubstitute;

namespace DjVisualizer.Application.Tests.Jobs;

public class GetJobDownloadUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IJobFileStorage _fileStorage = Substitute.For<IJobFileStorage>();

    private GetJobDownloadUseCase CreateSut() => new(_jobRepository, _fileStorage);

    private static Job CompletedJob()
    {
        var job = Job.Create(JobTitle.Create("Friday Night: Deep House Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        job.Start(Now);
        job.Complete(Now);
        return job;
    }

    [Fact]
    public async Task ExecuteAsync_Returns_NotFound_For_A_Malformed_Job_Id()
    {
        var result = await CreateSut().ExecuteAsync("not-a-guid", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_NotFound_When_No_Job_Exists()
    {
        var jobId = JobId.New();
        _jobRepository.FindAsync(jobId, Arg.Any<CancellationToken>()).Returns((Job?)null);

        var result = await CreateSut().ExecuteAsync(jobId.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_NotReady_When_The_Job_Has_Not_Completed()
    {
        var job = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotReady);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Failure_When_Completed_But_The_File_Is_Missing()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetOutputFilePathAsync(job.Id, Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_The_File_Path_And_A_Sanitized_Download_Name_On_Success()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetOutputFilePathAsync(job.Id, Arg.Any<CancellationToken>()).Returns("/data/jobs/x/output/video.mp4");

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FilePath.Should().Be("/data/jobs/x/output/video.mp4");
        result.Value.FileName.Should().Be("Friday Night_ Deep House Set.mp4");
    }

    /// <summary>
    /// Sanitisation must not depend on the server's operating system.
    /// <c>Path.GetInvalidFileNameChars()</c> returns about forty characters on Windows and only
    /// two on Linux, so using it meant this app produced one filename on the developer's machine
    /// and a different one in the Linux container it deploys to - which is exactly the direction
    /// that hides a bug, since the deployed behaviour is the untested one. It also sanitises for
    /// the wrong machine: the name is written to disk by the client, not the server.
    ///
    /// These characters are all legal in a Linux filename and all illegal on Windows, so a run
    /// on Linux fails this test if the platform-dependent call ever comes back.
    /// </summary>
    [Theory]
    [InlineData("Deep:House", "Deep_House.mp4")]
    [InlineData("What? Live", "What_ Live.mp4")]
    [InlineData("Mix*Tape", "Mix_Tape.mp4")]
    [InlineData(@"Rock\Roll", "Rock_Roll.mp4")]
    [InlineData("A<B>C", "A_B_C.mp4")]
    [InlineData("Say \"Hello\"", "Say _Hello_.mp4")]
    [InlineData("Left|Right", "Left_Right.mp4")]
    [InlineData("Slash/Burn", "Slash_Burn.mp4")]
    public async Task ExecuteAsync_Sanitizes_The_Same_Way_On_Every_Platform(string title, string expectedFileName)
    {
        var job = Job.Create(JobTitle.Create(title), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        job.Start(Now);
        job.Complete(Now);
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetOutputFilePathAsync(job.Id, Arg.Any<CancellationToken>()).Returns("/data/jobs/x/output/video.mp4");

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.Value!.FileName.Should().Be(expectedFileName);
    }
}
