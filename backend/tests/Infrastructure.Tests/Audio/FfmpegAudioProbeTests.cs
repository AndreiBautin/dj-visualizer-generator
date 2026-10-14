using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Tests.Support;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Audio;

public class FfmpegAudioProbeTests : IDisposable
{
    private readonly string _wavPath = Path.Combine(Path.GetTempPath(), $"djvisualizer-probe-{Guid.NewGuid():N}.wav");

    public void Dispose()
    {
        if (File.Exists(_wavPath))
        {
            File.Delete(_wavPath);
        }
    }

    [RequiresFfmpegFact]
    public async Task GetDurationAsync_Reports_The_Real_Duration_Of_A_Wav_File()
    {
        File.WriteAllBytes(_wavPath, SilentWavBuilder.Build(TimeSpan.FromSeconds(2), sampleRate: 8000));
        var sut = new FfmpegAudioProbe();

        var duration = await sut.GetDurationAsync(_wavPath, CancellationToken.None);

        duration.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    /// A ".wav" upload whose bytes are a concat script naming another file on the server. Sniffed,
    /// ffmpeg reads it as concat and probes the other file; forced to the wav demuxer it is
    /// rejected. The upload signature check would stop this particular file, but it reads only 16
    /// bytes - forcing the demuxer is what holds regardless.
    /// </summary>
    [RequiresFfmpegFact]
    public async Task GetDurationAsync_Reads_A_File_As_The_Format_Its_Extension_Claims_Not_As_A_Sniffed_Script()
    {
        var directory = Path.GetDirectoryName(_wavPath)!;
        var otherFile = Path.Combine(directory, $"djvisualizer-secret-{Guid.NewGuid():N}.wav");
        var script = Path.Combine(directory, $"djvisualizer-script-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(otherFile, SilentWavBuilder.Build(TimeSpan.FromSeconds(2), sampleRate: 8000));
        File.WriteAllText(script, $"ffconcat version 1.0\nfile '{Path.GetFileName(otherFile)}'\nduration 2\n");
        try
        {
            var act = () => new FfmpegAudioProbe().GetDurationAsync(script, CancellationToken.None);

            await act.Should().ThrowAsync<DjVisualizer.Application.Abstractions.AudioProbeException>();
        }
        finally
        {
            File.Delete(otherFile);
            File.Delete(script);
        }
    }

    [RequiresFfmpegFact]
    public async Task GetDurationAsync_Throws_An_AudioProbeException_For_A_Nonexistent_File()
    {
        var sut = new FfmpegAudioProbe();

        var act = () => sut.GetDurationAsync(Path.Combine(Path.GetTempPath(), "does-not-exist.wav"), CancellationToken.None);

        await act.Should().ThrowAsync<DjVisualizer.Application.Abstractions.AudioProbeException>();
    }
}
