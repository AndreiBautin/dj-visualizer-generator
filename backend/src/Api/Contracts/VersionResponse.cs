namespace DjVisualizer.Api.Contracts;

/// <summary>The commit this instance is running, or null when it cannot be determined.</summary>
public sealed record VersionResponse(string? Commit);
