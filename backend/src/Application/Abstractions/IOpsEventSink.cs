namespace DjVisualizer.Application.Abstractions;

public interface IOpsEventSink
{
    Task PublishAsync(OpsEvent evt, CancellationToken cancellationToken);
}
