using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class Job
{
    public JobId Id { get; }
    public JobTitle Title { get; }
    public VideoPreset Preset { get; }
    public RotationSpeed RotationSpeed { get; }
    public CaptionFont CaptionFont { get; }
    public JobStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int DownloadCount { get; private set; }

    /// <summary>
    /// How many times a completed job's video may be fetched before the id stops working.
    /// </summary>
    /// <remarks>
    /// A job id is a bearer token with no owner (docs/SECURITY.md), so without a ceiling one
    /// accepted render permits unbounded egress: the file sits on disk for the whole retention
    /// window and re-serving it costs no CPU, only bandwidth. Five is comfortably above what a
    /// visitor needs - the download, a retry, a second device - and far below the point where
    /// scripting the re-fetch is worth anyone's time.
    /// </remarks>
    public const int MaxDownloads = 5;

    public bool DownloadLimitReached => DownloadCount >= MaxDownloads;

    private Job(
        JobId id,
        JobTitle title,
        VideoPreset preset,
        RotationSpeed rotationSpeed,
        CaptionFont captionFont,
        JobStatus status,
        int progress,
        string? errorMessage,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int downloadCount)
    {
        Id = id;
        Title = title;
        Preset = preset;
        RotationSpeed = rotationSpeed;
        CaptionFont = captionFont;
        Status = status;
        Progress = progress;
        ErrorMessage = errorMessage;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DownloadCount = downloadCount;
    }

    public static Job Create(
        JobTitle title,
        VideoPreset preset,
        RotationSpeed rotationSpeed,
        CaptionFont captionFont,
        DateTimeOffset now) =>
        new(JobId.New(), title, preset, rotationSpeed, captionFont, JobStatus.Queued, progress: 0, errorMessage: null, createdAt: now, updatedAt: now, downloadCount: 0);

    public static Job Rehydrate(
        JobId id,
        JobTitle title,
        VideoPreset preset,
        RotationSpeed rotationSpeed,
        CaptionFont captionFont,
        JobStatus status,
        int progress,
        string? errorMessage,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int downloadCount = 0) =>
        new(id, title, preset, rotationSpeed, captionFont, status, progress, errorMessage, createdAt, updatedAt, downloadCount);

    public void Start(DateTimeOffset now)
    {
        EnsureTransitionAllowed(JobStatus.Processing, "start");
        Status = JobStatus.Processing;
        UpdatedAt = now;
    }

    public void UpdateProgress(int progress, DateTimeOffset now)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidJobStateTransitionException(Status.ToString(), "update the progress of");
        }

        if (progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Progress must be between 0 and 100.");
        }

        Progress = progress;
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureTransitionAllowed(JobStatus.Completed, "complete");
        Status = JobStatus.Completed;
        Progress = 100;
        UpdatedAt = now;
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        EnsureTransitionAllowed(JobStatus.Failed, "fail");
        Status = JobStatus.Failed;
        ErrorMessage = reason;
        UpdatedAt = now;
    }

    /// <summary>Counts one delivery of the rendered video against <see cref="MaxDownloads"/>.</summary>
    /// <remarks>
    /// Takes no timestamp, and deliberately does <em>not</em> touch <see cref="UpdatedAt"/>.
    /// Retention sweeps key on that timestamp, so bumping it here would let anyone holding the id
    /// keep a job - and its video - alive indefinitely by re-downloading inside the retention
    /// window. That is the exact opposite of what this limit is for.
    /// </remarks>
    public void RecordDownload()
    {
        if (Status != JobStatus.Completed)
        {
            throw new InvalidJobStateTransitionException(Status.ToString(), "download");
        }

        if (DownloadLimitReached)
        {
            throw new DownloadLimitExceededException(MaxDownloads);
        }

        DownloadCount++;
    }

    private void EnsureTransitionAllowed(JobStatus target, string attemptedAction)
    {
        var allowed = (Status, target) switch
        {
            (JobStatus.Queued, JobStatus.Processing) => true,
            (JobStatus.Processing, JobStatus.Completed) => true,
            (JobStatus.Queued, JobStatus.Failed) => true,
            (JobStatus.Processing, JobStatus.Failed) => true,
            _ => false,
        };

        if (!allowed)
        {
            throw new InvalidJobStateTransitionException(Status.ToString(), attemptedAction);
        }
    }
}
