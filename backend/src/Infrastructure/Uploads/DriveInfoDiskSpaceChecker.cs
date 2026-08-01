using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Uploads;

public sealed class DriveInfoDiskSpaceChecker(string rootPath) : IDiskSpaceChecker
{
    public long GetAvailableFreeBytes()
    {
        var fullPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(fullPath);

        var driveRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(driveRoot))
        {
            // Can't determine the drive - fail open rather than block every upload.
            return long.MaxValue;
        }

        return new DriveInfo(driveRoot).AvailableFreeSpace;
    }
}
