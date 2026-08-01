using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Jobs;

namespace DjVisualizer.Infrastructure.Uploads;

public sealed class FileSystemJobInputFileLocator(string rootPath) : IJobInputFileLocator
{
    private readonly string _rootPath = Path.GetFullPath(rootPath);

    public Task<JobInputFiles?> LocateAsync(JobId jobId, CancellationToken cancellationToken)
    {
        var inputDirectory = Path.Combine(JobPaths.GetJobDirectory(_rootPath, jobId), "input");
        if (!Directory.Exists(inputDirectory))
        {
            return Task.FromResult<JobInputFiles?>(null);
        }

        var audioPath = Directory.EnumerateFiles(inputDirectory, "audio.*").FirstOrDefault();
        var artworkPath = Directory.EnumerateFiles(inputDirectory, "artwork.*").FirstOrDefault();

        if (audioPath is null || artworkPath is null)
        {
            return Task.FromResult<JobInputFiles?>(null);
        }

        return Task.FromResult<JobInputFiles?>(new JobInputFiles(audioPath, artworkPath));
    }
}
