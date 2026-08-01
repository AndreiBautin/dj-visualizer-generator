using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Uploads;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DjVisualizer.Api.HealthChecks;

public sealed class DiskSpaceHealthCheck(IDiskSpaceChecker diskSpaceChecker, UploadLimits limits) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var freeBytes = diskSpaceChecker.GetAvailableFreeBytes();
        var data = new Dictionary<string, object> { ["freeBytes"] = freeBytes, ["minFreeBytes"] = limits.MinFreeDiskBytes };

        var result = freeBytes >= limits.MinFreeDiskBytes
            ? HealthCheckResult.Healthy("Sufficient disk space available.", data)
            : HealthCheckResult.Degraded("Available disk space is below the configured minimum.", data: data);

        return Task.FromResult(result);
    }
}
