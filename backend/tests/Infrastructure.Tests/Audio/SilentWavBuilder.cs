namespace DjVisualizer.Infrastructure.Tests.Audio;

/// <summary>Builds a minimal, valid, silent PCM WAV file in memory so ffprobe-dependent tests
/// don't need a binary fixture checked into the repo.</summary>
internal static class SilentWavBuilder
{
    public static byte[] Build(TimeSpan duration, int sampleRate, short bitsPerSample = 16, short channels = 1)
    {
        var numSamples = (int)(duration.TotalSeconds * sampleRate);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = numSamples * blockAlign;
        var byteRate = sampleRate * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);

        return stream.ToArray();
    }
}
