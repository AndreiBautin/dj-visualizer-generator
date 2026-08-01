using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Domain.Uploads;

namespace DjVisualizer.Application.Jobs;

public sealed class CreateJobUseCase(
    IJobQueue jobQueue,
    IJobFileStorage fileStorage,
    IAudioProbe audioProbe,
    IDiskSpaceChecker diskSpaceChecker,
    UploadLimits limits,
    IClock clock) : ICreateJobUseCase
{
    public async Task<Result<CreateJobResult>> ExecuteAsync(CreateJobRequest request, CancellationToken cancellationToken)
    {
        if (diskSpaceChecker.GetAvailableFreeBytes() < limits.MinFreeDiskBytes)
        {
            return Result<CreateJobResult>.Failure(new Error(
                ErrorCodes.Unavailable,
                "The server is low on disk space and cannot accept new uploads right now. Please try again shortly."));
        }

        JobTitle title;
        try
        {
            title = JobTitle.Create(request.Title);
        }
        catch (InvalidJobTitleException ex)
        {
            return Result<CreateJobResult>.Failure(Error.Validation(ex.Message));
        }

        VideoPreset preset;
        try
        {
            preset = VideoPreset.FromName(request.PresetName);
        }
        catch (InvalidVideoPresetException ex)
        {
            return Result<CreateJobResult>.Failure(Error.Validation(ex.Message));
        }

        RotationSpeed rotationSpeed;
        try
        {
            rotationSpeed = request.RotationSpeedSeconds.HasValue
                ? RotationSpeed.Create(request.RotationSpeedSeconds.Value)
                : RotationSpeed.Default;
        }
        catch (InvalidRotationSpeedException ex)
        {
            return Result<CreateJobResult>.Failure(Error.Validation(ex.Message));
        }

        CaptionFont captionFont;
        try
        {
            captionFont = string.IsNullOrWhiteSpace(request.CaptionFontName)
                ? CaptionFont.Default
                : CaptionFont.FromName(request.CaptionFontName);
        }
        catch (InvalidCaptionFontException ex)
        {
            return Result<CreateJobResult>.Failure(Error.Validation(ex.Message));
        }

        var job = Job.Create(title, preset, rotationSpeed, captionFont, clock.UtcNow);

        var audioResult = await fileStorage.SaveAudioAsync(job.Id, request.AudioStream, request.AudioFileName, cancellationToken);
        if (!audioResult.IsSuccess)
        {
            return Result<CreateJobResult>.Failure(audioResult.Error!);
        }

        var artworkResult = await fileStorage.SaveArtworkAsync(job.Id, request.ArtworkStream, request.ArtworkFileName, cancellationToken);
        if (!artworkResult.IsSuccess)
        {
            await fileStorage.DeleteJobFilesAsync(job.Id, cancellationToken);
            return Result<CreateJobResult>.Failure(artworkResult.Error!);
        }

        TimeSpan duration;
        try
        {
            duration = await audioProbe.GetDurationAsync(audioResult.Value!.AbsolutePath, cancellationToken);
        }
        catch (AudioProbeException ex)
        {
            await fileStorage.DeleteJobFilesAsync(job.Id, cancellationToken);
            return Result<CreateJobResult>.Failure(Error.Failure(ex.Message));
        }

        if (duration > TimeSpan.FromSeconds(limits.MaxDurationSeconds))
        {
            await fileStorage.DeleteJobFilesAsync(job.Id, cancellationToken);
            return Result<CreateJobResult>.Failure(Error.Validation(
                $"Audio duration {duration:hh\\:mm\\:ss} exceeds the maximum allowed duration of {TimeSpan.FromSeconds(limits.MaxDurationSeconds):hh\\:mm\\:ss}."));
        }

        await jobQueue.EnqueueAsync(job, cancellationToken);

        return Result<CreateJobResult>.Success(new CreateJobResult(job.Id));
    }
}
