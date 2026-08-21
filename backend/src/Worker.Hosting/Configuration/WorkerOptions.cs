namespace DjVisualizer.Worker.Configuration;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    // Bundled from assets/fonts/ (OFL-licensed, see the accompanying *-OFL.txt files) and copied
    // into the Worker image at this path by Dockerfile.worker - chosen to actually look good as a
    // bold title caption over video, rather than a generic system font.
    public string FontFilePathSansBold { get; set; } = "/app/fonts/Poppins-ExtraBold.ttf";
    public string FontFilePathSerifBold { get; set; } = "/app/fonts/AbrilFatface-Regular.ttf";
    public string FontFilePathMonoBold { get; set; } = "/app/fonts/SpaceMono-Bold.ttf";

    /// <summary>libx264 encoding preset - trades compression efficiency for encode speed. "veryfast"
    /// renders long DJ sets in a practical amount of time; "medium" (ffmpeg's own default) is
    /// noticeably slower for negligible visual difference on this kind of content. Only used when
    /// <see cref="VideoCodec"/> is "libx264".</summary>
    public string X264Preset { get; set; } = "veryfast";

    /// <summary>"libx264" (default - software encoding, works everywhere including Docker/CI with
    /// no GPU) or "h264_nvenc" (NVIDIA hardware encoding - much faster, but requires a compatible
    /// NVIDIA GPU and driver on the machine actually running the Worker).</summary>
    public string VideoCodec { get; set; } = "libx264";

    public int PollingIntervalSeconds { get; set; } = 3;
    public int CleanupIntervalSeconds { get; set; } = 300;
    public int RetentionMinutes { get; set; } = 60;
    public int StaleProcessingMinutes { get; set; } = 60;
}
