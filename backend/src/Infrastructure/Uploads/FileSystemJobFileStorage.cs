using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Jobs;

namespace DjVisualizer.Infrastructure.Uploads;

public sealed class FileSystemJobFileStorage(string rootPath, IFileSignatureValidator signatureValidator, UploadLimits limits)
    : IJobFileStorage
{
    private static readonly string[] AllowedAudioExtensions = [".mp3", ".wav", ".flac", ".m4a"];
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png"];
    private const int HeaderBufferSize = 16;

    private readonly string _rootPath = Path.GetFullPath(rootPath);

    public Task<Result<SavedFile>> SaveAudioAsync(JobId jobId, Stream content, string originalFileName, CancellationToken cancellationToken) =>
        SaveAsync(jobId, content, originalFileName, FileCategory.Audio, "audio", AllowedAudioExtensions, limits.MaxAudioBytes, cancellationToken);

    public Task<Result<SavedFile>> SaveArtworkAsync(JobId jobId, Stream content, string originalFileName, CancellationToken cancellationToken) =>
        SaveAsync(jobId, content, originalFileName, FileCategory.Image, "artwork", AllowedImageExtensions, limits.MaxImageBytes, cancellationToken);

    public Task DeleteJobFilesAsync(JobId jobId, CancellationToken cancellationToken)
    {
        var jobDirectory = JobPaths.GetJobDirectory(_rootPath, jobId);
        if (Directory.Exists(jobDirectory))
        {
            Directory.Delete(jobDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task<string> PrepareOutputFilePathAsync(JobId jobId, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(JobPaths.GetJobDirectory(_rootPath, jobId), "output");
        Directory.CreateDirectory(outputDirectory);
        return Task.FromResult(Path.Combine(outputDirectory, "video.mp4"));
    }

    public Task<RenderedVideo?> GetRenderedVideoAsync(JobId jobId, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(JobPaths.GetJobDirectory(_rootPath, jobId), "output", "video.mp4");
        var file = new FileInfo(outputPath);
        return Task.FromResult(file.Exists ? new RenderedVideo(outputPath, file.Length) : null);
    }

    private async Task<Result<SavedFile>> SaveAsync(
        JobId jobId,
        Stream content,
        string originalFileName,
        FileCategory category,
        string targetBaseName,
        string[] allowedExtensions,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return Result<SavedFile>.Failure(Error.Validation($"Unsupported file extension '{extension}'."));
        }

        var headerBuffer = new byte[HeaderBufferSize];
        var headerBytesRead = await ReadFullyAsync(content, headerBuffer, cancellationToken);
        var header = headerBuffer[..headerBytesRead];

        if (!signatureValidator.IsValid(category, extension, header))
        {
            return Result<SavedFile>.Failure(Error.Validation($"The uploaded {targetBaseName} file's contents do not match a supported format."));
        }

        if (headerBytesRead > maxBytes)
        {
            return Result<SavedFile>.Failure(Error.Validation($"The uploaded {targetBaseName} file exceeds the maximum allowed size."));
        }

        var jobDirectory = JobPaths.GetJobDirectory(_rootPath, jobId);
        var inputDirectory = Path.Combine(jobDirectory, "input");
        Directory.CreateDirectory(inputDirectory);
        var destinationPath = Path.Combine(inputDirectory, targetBaseName + extension);

        var totalBytes = (long)headerBytesRead;
        var tooLarge = false;
        var buffer = new byte[81920];

        await using (var destination = File.Create(destinationPath))
        {
            await destination.WriteAsync(header.AsMemory(0, headerBytesRead), cancellationToken);

            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    tooLarge = true;
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }

        if (tooLarge)
        {
            File.Delete(destinationPath);
            return Result<SavedFile>.Failure(Error.Validation($"The uploaded {targetBaseName} file exceeds the maximum allowed size."));
        }

        return Result<SavedFile>.Success(new SavedFile(destinationPath, totalBytes));
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
