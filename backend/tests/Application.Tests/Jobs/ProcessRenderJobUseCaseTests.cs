using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DjVisualizer.Application.Tests.Jobs;

public class ProcessRenderJobUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly JobInputFiles InputFiles = new("/data/jobs/x/input/audio.mp3", "/data/jobs/x/input/artwork.png");
    private const string OutputPath = "/data/jobs/x/output/video.mp4";

    private readonly IJobInputFileLocator _inputFileLocator = Substitute.For<IJobInputFileLocator>();
    private readonly IAudioProbe _audioProbe = Substitute.For<IAudioProbe>();
    private readonly IJobFileStorage _fileStorage = Substitute.For<IJobFileStorage>();
    private readonly IVideoRenderer _videoRenderer = Substitute.For<IVideoRenderer>();
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IOpsEventSink _ops = Substitute.For<IOpsEventSink>();

    public ProcessRenderJobUseCaseTests()
    {
        _clock.UtcNow.Returns(Now);
        _fileStorage.PrepareOutputFilePathAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(OutputPath);
    }

    private ProcessRenderJobUseCase CreateSut(IOpsEventSink? ops = null) =>
        new(_inputFileLocator, _audioProbe, _fileStorage, _videoRenderer, _jobRepository, _clock, NullLogger<ProcessRenderJobUseCase>.Instance, ops ?? _ops);

    private static Job CreateProcessingJob()
    {
        var job = Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.FullHd1080p, RotationSpeed.Create(4.5), CaptionFont.SerifBold, Now);
        job.Start(Now);
        return job;
    }

    [Fact]
    public async Task ExecuteAsync_Renders_Reports_Progress_And_Completes_The_Job_On_The_Happy_Path()
    {
        var job = CreateProcessingJob();
        var observedProgressAtEachSave = new List<int>();
        _jobRepository.SaveAsync(Arg.Do<Job>(j => observedProgressAtEachSave.Add(j.Progress)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns(InputFiles);
        _audioProbe.GetDurationAsync(InputFiles.AudioFilePath, Arg.Any<CancellationToken>()).Returns(TimeSpan.FromMinutes(45));
        _videoRenderer
            .RenderAsync(Arg.Any<RenderRequest>(), Arg.Any<RenderProgressCallback>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => ReportProgress(callInfo.Arg<RenderProgressCallback>()!, 50, 100));

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        await _videoRenderer.Received(1).RenderAsync(
            Arg.Is<RenderRequest>(r =>
                r!.AudioFilePath == InputFiles.AudioFilePath &&
                r.ArtworkFilePath == InputFiles.ArtworkFilePath &&
                r.OutputFilePath == OutputPath &&
                r.Preset == VideoPreset.FullHd1080p &&
                r.Title == "Friday Night Set" &&
                r.Duration == TimeSpan.FromMinutes(45) &&
                r.RotationPeriodSeconds == 4.5 &&
                r.CaptionFont == CaptionFont.SerifBold),
            Arg.Any<RenderProgressCallback>(),
            Arg.Any<CancellationToken>());
        // Progress is reported at 50, then 100 during rendering, then the job is saved once more as Completed (still 100).
        observedProgressAtEachSave.Should().Equal(50, 100, 100);
        await _ops.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    private static async Task ReportProgress(RenderProgressCallback onProgress, params int[] percentages)
    {
        foreach (var percent in percentages)
        {
            await onProgress(percent, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Fails_The_Job_When_Input_Files_Are_Missing()
    {
        var job = CreateProcessingJob();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns((JobInputFiles?)null);

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        await _videoRenderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
        await _jobRepository.Received(1).SaveAsync(Arg.Is<Job>(j => j!.Status == JobStatus.Failed), Arg.Any<CancellationToken>());
        await _ops.Received(1).PublishAsync(
            Arg.Is<OpsEvent>(e => e!.Service == "dj-worker" && e.Level == "error" && e.Message.Contains(job.Id.ToString(), StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Fails_The_Job_When_The_Audio_Cannot_Be_Probed()
    {
        var job = CreateProcessingJob();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns(InputFiles);
        _audioProbe.GetDurationAsync(InputFiles.AudioFilePath, Arg.Any<CancellationToken>())
            .Returns<TimeSpan>(_ => throw new AudioProbeException("ffprobe failed"));

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be(ProcessRenderJobUseCase.AudioUnreadableMessage);
        await _videoRenderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
        await _ops.Received(1).PublishAsync(
            Arg.Is<OpsEvent>(e => e!.Message.Contains(ProcessRenderJobUseCase.AudioUnreadableMessage, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Fails_The_Job_When_Rendering_Throws_A_RenderException()
    {
        var job = CreateProcessingJob();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns(InputFiles);
        _audioProbe.GetDurationAsync(InputFiles.AudioFilePath, Arg.Any<CancellationToken>()).Returns(TimeSpan.FromMinutes(45));
        _videoRenderer
            .RenderAsync(Arg.Any<RenderRequest>(), Arg.Any<RenderProgressCallback>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new RenderException("ffmpeg exited with code 1"));

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be(ProcessRenderJobUseCase.RenderFailedMessage);
        await _ops.Received(1).PublishAsync(
            Arg.Is<OpsEvent>(e => e!.Message.Contains(ProcessRenderJobUseCase.RenderFailedMessage, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Still_Fails_The_Job_When_The_Ops_Sink_Throws()
    {
        var job = CreateProcessingJob();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns((JobInputFiles?)null);
        _ops.PublishAsync(Arg.Any<OpsEvent>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("ops down"));

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        await _jobRepository.Received(1).SaveAsync(Arg.Is<Job>(j => j!.Status == JobStatus.Failed), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A job's ErrorMessage is served verbatim by GET /jobs/{id}, so it is a trust boundary on
    /// the way out. ffmpeg reports failures with the full command line, which embeds absolute
    /// server paths and the internal filter graph - none of that may reach a caller.
    /// </summary>
    [Theory]
    [InlineData(@"ffmpeg exited with code 1: Error opening C:\\Users\\dj\\jobs\\a1\\input\\audio.mp3")]
    [InlineData("ffprobe failed: /data/jobs/9f2/input/audio.mp3: Invalid data found")]
    public async Task ExecuteAsync_Does_Not_Leak_Renderer_Diagnostics_Into_The_Job_Error_Message(string diagnosticDetail)
    {
        var job = CreateProcessingJob();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns(InputFiles);
        _audioProbe.GetDurationAsync(InputFiles.AudioFilePath, Arg.Any<CancellationToken>()).Returns(TimeSpan.FromMinutes(45));
        _videoRenderer
            .RenderAsync(Arg.Any<RenderRequest>(), Arg.Any<RenderProgressCallback>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new RenderException(diagnosticDetail));

        await CreateSut().ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().NotContain(diagnosticDetail);
        job.ErrorMessage.Should().NotContainAny("/data/jobs", @"C:\Users", "ffmpeg", "ffprobe");
    }

    [Fact]
    public async Task ExecuteAsync_Leaves_The_Job_Processing_And_Rethrows_On_Graceful_Cancellation()
    {
        var job = CreateProcessingJob();
        using var cts = new CancellationTokenSource();
        _inputFileLocator.LocateAsync(job.Id, Arg.Any<CancellationToken>()).Returns(InputFiles);
        _audioProbe.GetDurationAsync(InputFiles.AudioFilePath, Arg.Any<CancellationToken>()).Returns(TimeSpan.FromMinutes(45));
        _videoRenderer
            .RenderAsync(Arg.Any<RenderRequest>(), Arg.Any<RenderProgressCallback>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var act = () => CreateSut().ExecuteAsync(job, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        job.Status.Should().Be(JobStatus.Processing);
        await _jobRepository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _ops.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }
}
