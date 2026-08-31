namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// A ceiling on how many bytes of rendered video the instance will serve in a rolling window.
/// </summary>
/// <remarks>
/// This is the only control in the system that bounds egress in absolute terms. The rate limiter
/// bounds requests per IP and <see cref="Domain.Jobs.Job.MaxDownloads"/> bounds re-fetches per
/// job, but neither caps the total: enough distinct callers creating enough distinct jobs adds up
/// without either one tripping. On metered hosting the total is what the bill is computed from,
/// so the total is what has to be capped.
/// </remarks>
public interface IEgressBudget
{
    /// <summary>
    /// Charges <paramref name="bytes"/> against the current window, returning false and charging
    /// nothing if that would exceed the budget. Reserving before serving rather than measuring
    /// after means a single very large response cannot overshoot.
    /// </summary>
    bool TryReserve(long bytes);

    /// <summary>Bytes still available in the current window; <see cref="long.MaxValue"/> when unlimited.</summary>
    long RemainingBytes { get; }
}
