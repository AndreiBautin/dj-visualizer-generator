using DjVisualizer.Infrastructure.Jobs;
using Microsoft.Extensions.Hosting;

namespace DjVisualizer.Worker;

public sealed class InstanceLeaseService(string root, string role) : IHostedService, IDisposable
{
    private FileSystemInstanceLease? _lease;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try { _lease = new FileSystemInstanceLease(root, role); }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"Only one {role} instance may use this jobs directory.", exception);
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() => _lease?.Dispose();
}
