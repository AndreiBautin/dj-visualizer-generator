using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// Streams an uploaded file to a job's input folder, validating extension, magic bytes, and
/// size against configured limits while streaming (never buffering the whole file in memory).
/// </summary>
public interface IJobFileStorage
{
    Task<Result<SavedFile>> SaveAudioAsync(JobId jobId, Stream content, string originalFileName, CancellationToken cancellationToken);

    Task<Result<SavedFile>> SaveArtworkAsync(JobId jobId, Stream content, string originalFileName, CancellationToken cancellationToken);

    Task DeleteJobFilesAsync(JobId jobId, CancellationToken cancellationToken);

    /// <summary>Ensures the job's output folder exists and returns the path the renderer should write video.mp4 to.</summary>
    Task<string> PrepareOutputFilePathAsync(JobId jobId, CancellationToken cancellationToken);

    /// <summary>Returns the path to the job's rendered video if it exists on disk, otherwise null.</summary>
    Task<string?> GetOutputFilePathAsync(JobId jobId, CancellationToken cancellationToken);
}
