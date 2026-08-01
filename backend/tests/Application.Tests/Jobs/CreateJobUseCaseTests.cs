using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Domain.Uploads;
using FluentAssertions;
using NSubstitute;

namespace DjVisualizer.Application.Tests.Jobs;

public class CreateJobUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly IJobFileStorage _fileStorage = Substitute.For<IJobFileStorage>();
    private readonly IAudioProbe _audioProbe = Substitute.For<IAudioProbe>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDiskSpaceChecker _diskSpaceChecker = Substitute.For<IDiskSpaceChecker>();
    private readonly UploadLimits _limits = new(maxAudioBytes: 1000, maxImageBytes: 1000, maxDurationSeconds: 21_600, minFreeDiskBytes: 1000);

    private CreateJobUseCase CreateSut() => new(_jobQueue, _fileStorage, _audioProbe, _diskSpaceChecker, _limits, _clock);

    private static CreateJobRequest ValidRequest() => new(
        Title: "Friday Night Set",
        PresetName: "1080p",
        AudioStream: new MemoryStream(),
        AudioFileName: "mix.mp3",
        ArtworkStream: new MemoryStream(),
        ArtworkFileName: "cover.jpg");

    public CreateJobUseCaseTests()
    {
        _clock.UtcNow.Returns(Now);
        _diskSpaceChecker.GetAvailableFreeBytes().Returns(long.MaxValue);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Success_And_Enqueues_The_Job_On_The_Happy_Path()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/audio.mp3", 500)));
        _fileStorage.SaveArtworkAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "cover.jpg", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/artwork.jpg", 200)));
        _audioProbe.GetDurationAsync("/data/jobs/x/input/audio.mp3", Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(90));

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<Job>(j => j!.Title.Value == "Friday Night Set" && j.Preset == VideoPreset.FullHd1080p && j.Status == JobStatus.Queued
                && j.RotationSpeed.SecondsPerRotation == RotationSpeed.DefaultSecondsPerRotation && j.CaptionFont == CaptionFont.Default),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Applies_A_Custom_RotationSpeed_And_CaptionFont_When_Provided()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/audio.mp3", 500)));
        _fileStorage.SaveArtworkAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "cover.jpg", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/artwork.jpg", 200)));
        _audioProbe.GetDurationAsync("/data/jobs/x/input/audio.mp3", Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(90));
        var request = ValidRequest() with { RotationSpeedSeconds = 6.0, CaptionFontName = "mono-bold" };

        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<Job>(j => j!.RotationSpeed.SecondsPerRotation == 6.0 && j.CaptionFont == CaptionFont.MonoBold),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_A_RotationSpeed_Outside_The_Allowed_Range()
    {
        var request = ValidRequest() with { RotationSpeedSeconds = 100.0 };

        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_An_Unknown_CaptionFontName()
    {
        var request = ValidRequest() with { CaptionFontName = "comic-sans" };

        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_The_Job_When_Free_Disk_Space_Is_Below_The_Configured_Minimum()
    {
        _diskSpaceChecker.GetAvailableFreeBytes().Returns(999); // limit is 1000

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Unavailable);
        await _fileStorage.DidNotReceiveWithAnyArgs().SaveAudioAsync(default, default!, default!, default);
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_An_Empty_Title_Without_Touching_Storage_Or_The_Queue()
    {
        var request = ValidRequest() with { Title = "   " };

        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
        await _fileStorage.DidNotReceiveWithAnyArgs().SaveAudioAsync(default, default!, default!, default);
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_An_Unknown_Preset()
    {
        var request = ValidRequest() with { PresetName = "4k" };

        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_Fast_When_The_Audio_File_Is_Invalid()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Failure(Error.Validation("unsupported audio format")));

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
        await _fileStorage.DidNotReceiveWithAnyArgs().SaveArtworkAsync(default, default!, default!, default);
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Cleans_Up_The_Audio_File_When_The_Artwork_Is_Invalid()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/audio.mp3", 500)));
        _fileStorage.SaveArtworkAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "cover.jpg", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Failure(Error.Validation("unsupported image format")));

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _fileStorage.Received(1).DeleteJobFilesAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>());
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Audio_Longer_Than_The_Configured_Maximum_Duration_And_Cleans_Up()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/audio.mp3", 500)));
        _fileStorage.SaveArtworkAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "cover.jpg", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/artwork.jpg", 200)));
        _audioProbe.GetDurationAsync("/data/jobs/x/input/audio.mp3", Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromHours(7));

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Validation);
        await _fileStorage.Received(1).DeleteJobFilesAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>());
        await _jobQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Maps_An_Unreadable_Audio_File_To_A_Failure_Result_And_Cleans_Up()
    {
        _fileStorage.SaveAudioAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "mix.mp3", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/audio.mp3", 500)));
        _fileStorage.SaveArtworkAsync(Arg.Any<JobId>(), Arg.Any<Stream>(), "cover.jpg", Arg.Any<CancellationToken>())
            .Returns(Result<SavedFile>.Success(new SavedFile("/data/jobs/x/input/artwork.jpg", 200)));
        _audioProbe.GetDurationAsync("/data/jobs/x/input/audio.mp3", Arg.Any<CancellationToken>())
            .Returns<TimeSpan>(_ => throw new AudioProbeException("ffprobe could not read the file"));

        var result = await CreateSut().ExecuteAsync(ValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Failure);
        await _fileStorage.Received(1).DeleteJobFilesAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>());
    }
}
