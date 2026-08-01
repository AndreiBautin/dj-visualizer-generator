using System.ComponentModel;
using System.Diagnostics;
using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Audio;

public sealed class FfmpegAudioProbe(string ffprobePath = "ffprobe") : IAudioProbe
{
    public async Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(filePath);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new AudioProbeException($"ffprobe could not be started: {ex.Message}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new AudioProbeException($"ffprobe exited with code {process.ExitCode}: {stderr}");
        }

        return FfprobeOutputParser.ParseDuration(stdout);
    }
}
