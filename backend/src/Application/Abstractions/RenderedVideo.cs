namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// A job's finished video on disk. Path and size travel together because they are read from one
/// stat of one file: fetching them separately would let the file be deleted by a retention sweep
/// between the two calls, and the size is what gets charged against the egress budget.
/// </summary>
public sealed record RenderedVideo(string FilePath, long SizeBytes);
