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
        DateTimeOffset updatedAt)
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
    }

    public static Job Create(
        JobTitle title,
        VideoPreset preset,
        RotationSpeed rotationSpeed,
        CaptionFont captionFont,
        DateTimeOffset now) =>
        new(JobId.New(), title, preset, rotationSpeed, captionFont, JobStatus.Queued, progress: 0, errorMessage: null, createdAt: now, updatedAt: now);

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
        DateTimeOffset updatedAt) =>
        new(id, title, preset, rotationSpeed, captionFont, status, progress, errorMessage, createdAt, updatedAt);

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
