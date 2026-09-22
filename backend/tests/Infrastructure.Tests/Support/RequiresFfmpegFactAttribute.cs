using System.Diagnostics;
using Xunit;

namespace DjVisualizer.Infrastructure.Tests.Support;

/// <summary>An xUnit fact that auto-skips on machines without ffmpeg/ffprobe on PATH, so the
/// suite stays green in dev sandboxes while still running for real in CI (which installs ffmpeg).</summary>
public sealed class RequiresFfmpegFactAttribute : FactAttribute
{
    public RequiresFfmpegFactAttribute()
    {
        if (!FfmpegAvailability.IsAvailable)
        {
            Skip = "ffprobe is not installed on this machine.";
        }
    }
}

internal static class FfmpegAvailability
{
    public static readonly bool IsAvailable = Check();

    private static bool Check()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ffprobe", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return false;
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }
            Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
