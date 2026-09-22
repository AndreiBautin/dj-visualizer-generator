namespace DjVisualizer.Infrastructure.Ops;

public sealed class IncidentBrainOpsOptions
{
    public const string SectionName = "Ops";

    public string? IncidentBrainUrl { get; init; }
    public string? IngestKey { get; init; }
    public string Service { get; init; } = "dj-worker";
}
