using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DjVisualizer.Infrastructure.Processes;

/// <summary>What a finished process left behind. <see cref="StandardOutput"/> is empty when the
/// caller consumed output line by line instead.</summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// The one place ffmpeg and ffprobe are started. Three hand-written copies of this had already
/// drifted: one did not kill ffprobe on cancellation, so a cancelled probe left the process
/// running. Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a shell.
/// </summary>
internal static class ProcessRunner
{
    /// <summary>Thrown when the executable could not be started at all (not installed, not on PATH).</summary>
    internal sealed class StartException(string fileName, Exception inner)
        : Exception($"{fileName} could not be started: {inner.Message}", inner);

    /// <param name="onOutputLine">When given, standard output is streamed to it line by line
    /// (ffmpeg's -progress pipe); otherwise it is collected into the result.</param>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task>? onOutputLine = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                }
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new StartException(fileName, ex);
        }

        process.BeginErrorReadLine();

        var stdout = string.Empty;
        try
        {
            if (onOutputLine is null)
            {
                stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            }
            else
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
                {
                    await onOutputLine(line, cancellationToken);
                }
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }

        lock (stderr)
        {
            return new ProcessResult(process.ExitCode, stdout, stderr.ToString());
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill.
        }
        catch (Win32Exception)
        {
            // Best effort during cancellation.
        }
    }
}
