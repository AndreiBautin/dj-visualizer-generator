namespace DjVisualizer.Domain.Exceptions;

public sealed class DownloadLimitExceededException(int maxDownloads)
    : DomainException($"This video has already been downloaded {maxDownloads} times.");
