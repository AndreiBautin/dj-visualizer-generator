namespace DjVisualizer.Infrastructure.Rendering;

/// <summary>
/// Input options for an uploaded file. Left to itself ffmpeg picks a demuxer by sniffing the
/// contents, not by the extension the upload was validated against: an upload named .mp3 whose
/// bytes read as a concat script was handed to the concat demuxer, and concat and playlist
/// formats can open other files. The signature check reads only 16 bytes, so it cannot rule that
/// out. Forcing the demuxer the extension promised, and allowing only the file protocol, means an
/// upload is read as what it claimed to be or not at all.
/// </summary>
internal static class FfmpegInputFormat
{
    public static IReadOnlyList<string> For(string path)
    {
        var demuxer = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "mp3",
            ".wav" => "wav",
            ".flac" => "flac",
            ".m4a" => "mov",
            ".png" => "png_pipe",
            ".jpg" or ".jpeg" => "jpeg_pipe",
            _ => null,
        };

        return demuxer is null
            ? ["-protocol_whitelist", "file"]
            : ["-protocol_whitelist", "file", "-f", demuxer];
    }
}
