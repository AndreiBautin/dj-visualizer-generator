using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Uploads;

public sealed class FileSignatureValidator : IFileSignatureValidator
{
    public bool IsValid(FileCategory category, string fileExtension, byte[] header)
    {
        var extension = fileExtension.ToLowerInvariant();

        return category switch
        {
            FileCategory.Audio => extension switch
            {
                ".mp3" => IsMp3(header),
                ".wav" => IsWav(header),
                ".flac" => IsFlac(header),
                ".m4a" => IsM4a(header),
                _ => false,
            },
            FileCategory.Image => extension switch
            {
                ".jpg" or ".jpeg" => IsJpeg(header),
                ".png" => IsPng(header),
                _ => false,
            },
            _ => false,
        };
    }

    private static bool IsMp3(byte[] h) =>
        (h.Length >= 3 && h[0] == 0x49 && h[1] == 0x44 && h[2] == 0x33) // "ID3" tag
        || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0); // MPEG frame sync

    private static bool IsWav(byte[] h) =>
        h.Length >= 12
        && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
        && h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E';

    private static bool IsFlac(byte[] h) =>
        h.Length >= 4 && h[0] == 'f' && h[1] == 'L' && h[2] == 'a' && h[3] == 'C';

    private static bool IsM4a(byte[] h) =>
        h.Length >= 8 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p';

    private static bool IsJpeg(byte[] h) =>
        h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;

    private static bool IsPng(byte[] h) =>
        h.Length >= 8
        && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
        && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A;
}
