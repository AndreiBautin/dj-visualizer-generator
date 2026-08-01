namespace DjVisualizer.Application.Abstractions;

public interface IVideoRenderer
{
    Task RenderAsync(RenderRequest request, RenderProgressCallback onProgress, CancellationToken cancellationToken);
}
