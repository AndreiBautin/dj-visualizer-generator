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

    [RequiresFfmpegFact]
    public async Task GetDurationAsync_Throws_An_AudioProbeException_For_A_Nonexistent_File()
    {
        var sut = new FfmpegAudioProbe();

        var act = () => sut.GetDurationAsync(Path.Combine(Path.GetTempPath(), "does-not-exist.wav"), CancellationToken.None);

        await act.Should().ThrowAsync<DjVisualizer.Application.Abstractions.AudioProbeException>();
    }
}
