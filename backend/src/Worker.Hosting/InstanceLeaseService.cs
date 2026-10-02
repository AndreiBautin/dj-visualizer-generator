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
    /// <summary>Released on a graceful stop rather than waiting for the container to be disposed,
    /// which can trail the stop by some time: until then the lock file cannot be deleted, and the
    /// API test host intermittently failed to clean up its jobs directory on Windows.</summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _lease?.Dispose();
        _lease = null;
    }
}
