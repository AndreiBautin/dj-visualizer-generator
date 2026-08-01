using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Infrastructure.Jobs;

internal static class JobPaths
{
    public static string GetJobDirectory(string rootPath, JobId id)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var directory = Path.GetFullPath(Path.Combine(normalizedRoot, id.ToString()));

        if (!directory.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved job directory escapes the jobs root.");
        }

        return directory;
    }
}
