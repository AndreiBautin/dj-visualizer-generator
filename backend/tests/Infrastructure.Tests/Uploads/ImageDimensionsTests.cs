using System.Diagnostics;
using DjVisualizer.Infrastructure.Uploads;
using DjVisualizer.Infrastructure.Tests.Support;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Uploads;

public class ImageDimensionsTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"djvisualizer-dims-{Guid.NewGuid():N}");

    public ImageDimensionsTests() => Directory.CreateDirectory(_workDir);

    public void Dispose() => Directory.Delete(_workDir, recursive: true);

    /// <summary>A header claiming 30000x30000: what a few-megabyte decompression bomb looks like.</summary>
    [Fact]
    public void Reads_A_Png_Header_Without_Decoding_The_Image()
    {
        byte[] header =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x75, 0x30, 0x00, 0x00, 0x75, 0x30,
        ];

        ImageDimensions.TryRead(new MemoryStream(header), ".png").Should().Be((30000, 30000));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    public void Garbage_Has_No_Dimensions(string extension) =>
        ImageDimensions.TryRead(new MemoryStream([0x89, 0x50, 0xFF, 0xD8, 0x01]), extension).Should().BeNull();

    [RequiresFfmpegTheory]
    [InlineData("png")]
    [InlineData("jpg")]
    public async Task Reads_The_Dimensions_Of_A_Real_Encoded_Image(string format)
    {
        var path = Path.Combine(_workDir, $"image.{format}");
        var startInfo = new ProcessStartInfo("ffmpeg") { UseShellExecute = false };
        foreach (var arg in new[] { "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=s=321x123", "-frames:v", "1", path })
        {
            startInfo.ArgumentList.Add(arg);
        }

        using (var process = Process.Start(startInfo)!)
        {
            await process.WaitForExitAsync();
            process.ExitCode.Should().Be(0);
        }

        await using var stream = File.OpenRead(path);
        ImageDimensions.TryRead(stream, "." + format).Should().Be((321, 123));
    }
}
