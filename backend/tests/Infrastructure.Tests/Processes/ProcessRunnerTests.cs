using System.Diagnostics;
using DjVisualizer.Infrastructure.Processes;
using DjVisualizer.Infrastructure.Tests.Support;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Processes;

public class ProcessRunnerTests
{
    /// <summary>One of the three copies this replaced did not kill ffprobe when cancelled, so a
    /// cancelled probe left the process running after the caller had moved on.</summary>
    [RequiresFfmpegFact]
    public async Task Cancelling_Kills_The_Process_Instead_Of_Leaving_It_Running()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var started = Stopwatch.StartNew();

        var act = () => ProcessRunner.RunAsync(
            "ffmpeg",
            ["-v", "error", "-re", "-f", "lavfi", "-i", "anullsrc", "-t", "60", "-f", "null", "-"],
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the 60 s process must not have been waited out");
    }

    [RequiresFfmpegFact]
    public async Task Reports_The_Exit_Code_And_Standard_Error_Of_A_Failed_Run()
    {
        var result = await ProcessRunner.RunAsync("ffprobe", ["-v", "error", "no-such-file.wav"], CancellationToken.None);

        result.ExitCode.Should().NotBe(0);
        result.StandardError.Should().Contain("no-such-file.wav");
    }

    [Fact]
    public async Task A_Missing_Executable_Is_A_Start_Failure_Not_An_Exit_Code()
    {
        var act = () => ProcessRunner.RunAsync("definitely-not-installed-" + Guid.NewGuid().ToString("N"), [], CancellationToken.None);

        await act.Should().ThrowAsync<ProcessRunner.StartException>();
    }
}
