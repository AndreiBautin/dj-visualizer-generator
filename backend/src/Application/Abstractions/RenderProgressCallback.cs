namespace DjVisualizer.Application.Abstractions;

/// <summary>Reports a 0-100 progress percentage; awaited so the caller can persist it before the
/// renderer moves on, without blocking the render's own async pipeline synchronously.</summary>
public delegate Task RenderProgressCallback(int percent, CancellationToken cancellationToken);
