using DjVisualizer.Application.Abstractions;
using DjVisualizer.Infrastructure.Processes;

namespace DjVisualizer.Infrastructure.Audio;

public sealed class FfmpegAudioProbe(string ffprobePath = "ffprobe") : IAudioProbe
{
    public async Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken)
    {
        string[] arguments = ["-v", "error", .. Rendering.FfmpegInputFormat.For(filePath), "-show_entries", "format=duration", "-of", "json", filePath];
        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(ffprobePath, arguments, cancellationToken);
        }
        catch (ProcessRunner.StartException ex)
        {
            throw new AudioProbeException(ex.Message);
        }

        if (result.ExitCode != 0)
        {
            throw new AudioProbeException($"ffprobe exited with code {result.ExitCode}: {result.StandardError}");
        }

        return FfprobeOutputParser.ParseDuration(result.StandardOutput);
    }
}
