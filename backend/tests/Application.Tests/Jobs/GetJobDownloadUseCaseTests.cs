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
    private const long VideoBytes = 12_500_000;

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IJobFileStorage _fileStorage = Substitute.For<IJobFileStorage>();
    private readonly IEgressBudget _egressBudget = Substitute.For<IEgressBudget>();

    public GetJobDownloadUseCaseTests() => _egressBudget.TryReserve(Arg.Any<long>()).Returns(true);

    private GetJobDownloadUseCase CreateSut() => new(_jobRepository, _fileStorage, _egressBudget, new JobDownloadGate());

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
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns((RenderedVideo?)null);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_The_File_Path_And_A_Sanitized_Download_Name_On_Success()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FilePath.Should().Be("/data/jobs/x/output/video.mp4");
        result.Value.FileName.Should().Be("Friday Night_ Deep House Set.mp4");
    }

    /// <summary>
    /// The amplification this whole limit exists for: without it, one accepted render permits
    /// unbounded egress, because the file is already on disk and re-serving it costs no CPU.
    /// Asserting on the sixth call specifically, rather than on "some call eventually fails",
    /// because an off-by-one that allowed six would still pass a vaguer test.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Refuses_The_Download_Past_The_Per_Job_Limit()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));

        for (var i = 0; i < Job.MaxDownloads; i++)
        {
            var allowed = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);
            allowed.IsSuccess.Should().BeTrue($"download {i + 1} is within the allowance of {Job.MaxDownloads}");
        }

        var refused = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        refused.IsSuccess.Should().BeFalse();
        refused.Error!.Code.Should().Be(ErrorCodes.Exhausted);
    }

    /// <summary>
    /// The count has to survive the process, since the whole point is to limit a job id that
    /// outlives any one request. It lives on the job, so the job must be written back.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Persists_The_Incremented_Download_Count()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));

        await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        job.DownloadCount.Should().Be(1);
        await _jobRepository.Received(1).SaveAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Unavailable_When_The_Egress_Budget_Is_Spent()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));
        _egressBudget.TryReserve(Arg.Any<long>()).Returns(false);

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Unavailable);
    }

    [Fact]
    public async Task ExecuteAsync_Charges_The_Egress_Budget_The_Actual_Size_Of_The_Video()
    {
        var job = CompletedJob();
        _jobRepository.FindAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));

        await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        _egressBudget.Received(1).TryReserve(VideoBytes);
    }

    /// <summary>
    /// A refused download must cost nothing, or a caller could exhaust the instance's budget with
    /// requests that never transfer a byte - turning the guard into the denial of service it is
    /// meant to prevent.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Does_Not_Charge_The_Budget_For_A_Job_That_Cannot_Be_Downloaded()
    {
        var queued = Job.Create(JobTitle.Create("Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        _jobRepository.FindAsync(queued.Id, Arg.Any<CancellationToken>()).Returns(queued);

        await CreateSut().ExecuteAsync(queued.Id.ToString(), CancellationToken.None);
        await CreateSut().ExecuteAsync(JobId.New().ToString(), CancellationToken.None);
        await CreateSut().ExecuteAsync("not-a-guid", CancellationToken.None);

        _egressBudget.DidNotReceive().TryReserve(Arg.Any<long>());
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
        _fileStorage.GetRenderedVideoAsync(job.Id, Arg.Any<CancellationToken>()).Returns(new RenderedVideo("/data/jobs/x/output/video.mp4", VideoBytes));

        var result = await CreateSut().ExecuteAsync(job.Id.ToString(), CancellationToken.None);

        result.Value!.FileName.Should().Be(expectedFileName);
    }
}
