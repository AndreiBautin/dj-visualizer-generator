using DjVisualizer.Application.Abstractions;
using DjVisualizer.Infrastructure.Uploads;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Uploads;

public class FileSignatureValidatorTests
{
    private readonly FileSignatureValidator _sut = new();

    public static readonly byte[] Id3Mp3Header = [0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    public static readonly byte[] FrameSyncMp3Header = [0xFF, 0xFB, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    public static readonly byte[] WavHeader = "RIFF\0\0\0\0WAVEfmt "u8.ToArray();
    public static readonly byte[] FlacHeader = "fLaC\0\0\0\0\0\0\0\0"u8.ToArray();
    public static readonly byte[] M4aHeader = [0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'M', (byte)'4', (byte)'A', 0x20];
    public static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    public static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public static readonly byte[] GarbageHeader = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    [Theory]
    [MemberData(nameof(ValidAudioCases))]
    public void IsValid_Accepts_Correctly_Signed_Audio_Files(string extension, byte[] header)
    {
        _sut.IsValid(FileCategory.Audio, extension, header).Should().BeTrue();
    }

    public static TheoryData<string, byte[]> ValidAudioCases() => new()
    {
        { ".mp3", Id3Mp3Header },
        { ".mp3", FrameSyncMp3Header },
        { ".wav", WavHeader },
        { ".flac", FlacHeader },
        { ".m4a", M4aHeader },
    };

    [Theory]
    [MemberData(nameof(ValidImageCases))]
    public void IsValid_Accepts_Correctly_Signed_Image_Files(string extension, byte[] header)
    {
        _sut.IsValid(FileCategory.Image, extension, header).Should().BeTrue();
    }

    public static TheoryData<string, byte[]> ValidImageCases() => new()
    {
        { ".jpg", JpegHeader },
        { ".jpeg", JpegHeader },
        { ".png", PngHeader },
    };

    [Fact]
    public void IsValid_Rejects_A_File_Renamed_To_Look_Like_Audio()
    {
        _sut.IsValid(FileCategory.Audio, ".mp3", GarbageHeader).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Rejects_A_File_Renamed_To_Look_Like_An_Image()
    {
        _sut.IsValid(FileCategory.Image, ".png", GarbageHeader).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Rejects_An_Image_Signature_Presented_As_Audio()
    {
        _sut.IsValid(FileCategory.Audio, ".mp3", PngHeader).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Rejects_An_Unsupported_Extension_Even_With_A_Valid_Signature()
    {
        _sut.IsValid(FileCategory.Audio, ".exe", Id3Mp3Header).Should().BeFalse();
    }
}
