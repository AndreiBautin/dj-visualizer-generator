namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// Default. Used when Incident Intelligence is not configured. Publish is a completed task.
/// </summary>
public sealed class NoOpOpsEventSink : IOpsEventSink
{
    public Task PublishAsync(OpsEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
}
