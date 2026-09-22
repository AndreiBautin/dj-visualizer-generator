namespace DjVisualizer.Api.Configuration;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    public string RootPath { get; set; } = "";

    /// <summary>
    /// When true, the API also hosts the render worker's background services in its own process
    /// instead of expecting a separate Worker container, and serves the built frontend as static
    /// files. Set for single-container hosting (see docs/DEPLOYMENT.md); left false for
    /// docker-compose, where the API, Worker and nginx are three separate containers.
    /// </summary>
    public bool SingleContainer { get; set; }
    public long MaxAudioBytes { get; set; } = 2_147_483_648; // 2 GB
    public long MaxImageBytes { get; set; } = 26_214_400; // 25 MB
    public int MaxDurationSeconds { get; set; } = 21_600; // 6 hours
    public long MinFreeDiskBytes { get; set; } = 3_221_225_472; // 3 GB - a safety margin above one worst-case upload

    /// <summary>
    /// Ceiling on rendered video served per <see cref="EgressWindowHours"/>. Zero means unlimited,
    /// which is the self-hosted default: on your own hardware bandwidth is not metered and a cap
    /// would only break a legitimate download. Metered hosting sets it - see render.yaml, where it
    /// sits below the plan's included allowance so overage is unreachable rather than merely
    /// unlikely.
    /// </summary>
    public long MaxEgressBytesPerWindow { get; set; }

    public int EgressWindowHours { get; set; } = 24;
}
