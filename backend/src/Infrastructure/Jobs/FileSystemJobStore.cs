using System.Text.Json;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Jobs;

/// <summary>
/// Persists job state as one status.json file per job folder under the shared jobs root.
/// Acts as both the repository (lookup/save by id) and the queue (enqueue/dequeue) since
/// both are backed by the same physical store; dequeuing scans this same root for folders
/// whose status.json reports Queued.
/// </summary>
public sealed class FileSystemJobStore : IJobRepository, IJobQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _rootPath;
    private readonly IClock _clock;

    public FileSystemJobStore(string rootPath, IClock clock)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _clock = clock;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<Job?> FindAsync(JobId id, CancellationToken cancellationToken)
    {
        var statusFilePath = GetStatusFilePath(id);
        if (!File.Exists(statusFilePath))
        {
            return null;
        }

        await using var stream = new FileStream(statusFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var dto = await JsonSerializer.DeserializeAsync<JobDto>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException($"status.json for job {id} was empty or malformed.");

        return ToDomain(dto);
    }

    public Task SaveAsync(Job job, CancellationToken cancellationToken) => WriteAsync(job, cancellationToken);

    public Task EnqueueAsync(Job job, CancellationToken cancellationToken) => WriteAsync(job, cancellationToken);

    public async Task<Job?> TryDequeueNextAsync(CancellationToken cancellationToken)
    {
        Job? oldestQueued = null;

        await foreach (var candidate in ReadAllReadableAsync(cancellationToken))
        {
            if (candidate.Status == JobStatus.Queued && (oldestQueued is null || candidate.CreatedAt < oldestQueued.CreatedAt))
            {
                oldestQueued = candidate;
            }
        }

        if (oldestQueued is null)
        {
            return null;
        }

        oldestQueued.Start(_clock.UtcNow);
        await WriteAsync(oldestQueued, cancellationToken);
        return oldestQueued;
    }

    public IAsyncEnumerable<Job> GetAllAsync(CancellationToken cancellationToken) => ReadAllReadableAsync(cancellationToken);

    /// <summary>
    /// Every job whose status.json can be read as a valid job. A file that cannot - missing,
    /// half-written, valid JSON carrying a value the domain rejects, or deleted mid-read - is
    /// skipped rather than thrown: both the queue and cleanup scan the whole root, so one bad file
    /// throwing here stopped every job from ever rendering and every sweep at that folder.
    /// <see cref="DeleteUnreadableFoldersAsync"/> is what eventually removes such folders.
    /// </summary>
    private async IAsyncEnumerable<Job> ReadAllReadableAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var jobDirectory in Directory.EnumerateDirectories(_rootPath))
        {
            var job = await TryReadAsync(Path.Combine(jobDirectory, "status.json"), cancellationToken);
            if (job is not null)
            {
                yield return job;
            }
        }
    }

    private static async Task<Job?> TryReadAsync(string statusFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(statusFilePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(statusFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var dto = await JsonSerializer.DeserializeAsync<JobDto>(stream, SerializerOptions, cancellationToken);
            return dto is null ? null : ToDomain(dto);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or ArgumentException or FormatException or FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task<int> DeleteUnreadableFoldersAsync(TimeSpan untouchedFor, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var cutoff = _clock.UtcNow - untouchedFor;
        foreach (var jobDirectory in Directory.EnumerateDirectories(_rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryReadAsync(Path.Combine(jobDirectory, "status.json"), cancellationToken) is not null)
            {
                continue;
            }

            // The newest write anywhere inside, not the folder's own timestamp: a folder's time
            // changes when an entry is added, not while a file in it grows, so a long upload
            // would otherwise look abandoned partway through.
            var lastWrite = new DirectoryInfo(jobDirectory)
                .EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                .Select(entry => entry.LastWriteTimeUtc)
                .Append(Directory.GetLastWriteTimeUtc(jobDirectory))
                .Max();
            if (lastWrite > cutoff.UtcDateTime)
            {
                continue;
            }

            Directory.Delete(jobDirectory, recursive: true);
            deleted++;
        }

        return deleted;
    }

    public Task DeleteAsync(JobId id, CancellationToken cancellationToken)
    {
        var jobDirectory = JobPaths.GetJobDirectory(_rootPath, id);
        if (Directory.Exists(jobDirectory))
        {
            Directory.Delete(jobDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAsync(Job job, CancellationToken cancellationToken)
    {
        var jobDirectory = JobPaths.GetJobDirectory(_rootPath, job.Id);
        Directory.CreateDirectory(jobDirectory);

        var statusFilePath = GetStatusFilePath(job.Id);
        var tempFilePath = Path.Combine(jobDirectory, Path.GetRandomFileName() + ".tmp");

        var dto = ToDto(job);
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
        }

        File.Move(tempFilePath, statusFilePath, overwrite: true);
    }

    private string GetStatusFilePath(JobId id) => Path.Combine(JobPaths.GetJobDirectory(_rootPath, id), "status.json");

    private static JobDto ToDto(Job job) => new(
        job.Id.ToString(),
        job.Title.Value,
        job.Preset.Name,
        job.RotationSpeed.SecondsPerRotation,
        job.CaptionFont.Name,
        job.Status.ToString(),
        job.Progress,
        job.ErrorMessage,
        job.CreatedAt,
        job.UpdatedAt,
        job.DownloadCount,
        job.LabelCrop.FocusX,
        job.LabelCrop.FocusY,
        job.LabelCrop.Zoom);

    private static Job ToDomain(JobDto dto) => Job.Rehydrate(
        JobId.Parse(dto.Id),
        JobTitle.Create(dto.Title),
        VideoPreset.FromName(dto.Preset),
        // Job files written before rotation speed / caption font existed lack these properties;
        // System.Text.Json leaves them at their CLR defaults (0.0 / null) rather than failing to
        // deserialize, so treat those defaults as "not set" instead of an invalid domain value.
        dto.RotationSpeedSecondsPerRotation is >= RotationSpeed.MinSecondsPerRotation and <= RotationSpeed.MaxSecondsPerRotation
            ? RotationSpeed.Create(dto.RotationSpeedSecondsPerRotation)
            : RotationSpeed.Default,
        string.IsNullOrWhiteSpace(dto.CaptionFont) ? CaptionFont.Default : CaptionFont.FromName(dto.CaptionFont),
        Enum.Parse<JobStatus>(dto.Status),
        dto.Progress,
        dto.ErrorMessage,
        dto.CreatedAt,
        dto.UpdatedAt,
        dto.DownloadCount,
        dto is { LabelFocusX: { } fx, LabelFocusY: { } fy, LabelZoom: { } zoom }
            ? LabelCrop.Create(fx, fy, zoom)
            : LabelCrop.Default);
}
