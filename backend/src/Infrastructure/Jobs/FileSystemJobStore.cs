using System.Text.Json;
using DjVisualizer.Application.Abstractions;
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

        await using var stream = File.OpenRead(statusFilePath);
        var dto = await JsonSerializer.DeserializeAsync<JobDto>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException($"status.json for job {id} was empty or malformed.");

        return ToDomain(dto);
    }

    public Task SaveAsync(Job job, CancellationToken cancellationToken) => WriteAsync(job, cancellationToken);

    public Task EnqueueAsync(Job job, CancellationToken cancellationToken) => WriteAsync(job, cancellationToken);

    public async Task<Job?> TryDequeueNextAsync(CancellationToken cancellationToken)
    {
        Job? oldestQueued = null;

        foreach (var jobDirectory in Directory.EnumerateDirectories(_rootPath))
        {
            var statusFilePath = Path.Combine(jobDirectory, "status.json");
            if (!File.Exists(statusFilePath))
            {
                continue;
            }

            JobDto? dto;
            try
            {
                await using var stream = File.OpenRead(statusFilePath);
                dto = await JsonSerializer.DeserializeAsync<JobDto>(stream, SerializerOptions, cancellationToken);
            }
            catch (JsonException)
            {
                continue; // a corrupt/partially-written status file shouldn't halt the whole poll
            }

            if (dto is null || !string.Equals(dto.Status, JobStatus.Queued.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = ToDomain(dto);
            if (oldestQueued is null || candidate.CreatedAt < oldestQueued.CreatedAt)
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

    public async IAsyncEnumerable<Job> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var jobDirectory in Directory.EnumerateDirectories(_rootPath))
        {
            var statusFilePath = Path.Combine(jobDirectory, "status.json");
            if (!File.Exists(statusFilePath))
            {
                continue;
            }

            JobDto? dto;
            try
            {
                await using var stream = File.OpenRead(statusFilePath);
                dto = await JsonSerializer.DeserializeAsync<JobDto>(stream, SerializerOptions, cancellationToken);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto is null)
            {
                continue;
            }

            yield return ToDomain(dto);
        }
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
        var tempFilePath = statusFilePath + ".tmp";

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
        job.DownloadCount);

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
        dto.DownloadCount);
}
