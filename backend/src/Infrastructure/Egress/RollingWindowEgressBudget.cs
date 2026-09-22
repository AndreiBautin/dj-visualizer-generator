using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Egress;

/// <summary>
/// In-memory <see cref="IEgressBudget"/> over a fixed-length window that resets the first time it
/// is consulted after the window has elapsed.
/// </summary>
/// <remarks>
/// <para>
/// In-memory rather than persisted, which means a restart forgives the spend so far. That is a
/// deliberate trade rather than an oversight: persisting the counter would need a write on the
/// hot path of every download, and the failure it guards against - a restart loop resetting the
/// budget repeatedly - would already be visible as an unhealthy service. On the single-container
/// deployment the process is the only thing serving video, so one counter sees every byte.
/// </para>
/// <para>
/// The window is a sliding reset, not a calendar day: it starts at construction and again
/// whenever <see cref="TryReserve"/> observes that a full window has passed. This avoids needing
/// a timezone, and avoids a scheduled task whose only job is to zero a number.
/// </para>
/// </remarks>
public sealed class RollingWindowEgressBudget : IEgressBudget
{
    private readonly long _maxBytesPerWindow;
    private readonly TimeSpan _window;
    private readonly IClock _clock;
    private readonly Lock _gate = new();

    private DateTimeOffset _windowStart;
    private long _reservedBytes;

    /// <param name="maxBytesPerWindow">Zero or negative means unlimited - the self-hosted default.</param>
    public RollingWindowEgressBudget(long maxBytesPerWindow, TimeSpan window, IClock clock)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "The egress window must be positive.");
        }

        _maxBytesPerWindow = maxBytesPerWindow;
        _window = window;
        _clock = clock;
        _windowStart = clock.UtcNow;
    }

    public bool IsUnlimited => _maxBytesPerWindow <= 0;

    public long RemainingBytes
    {
        get
        {
            if (IsUnlimited)
            {
                return long.MaxValue;
            }

            lock (_gate)
            {
                RollWindowIfElapsed(_clock.UtcNow);
                return Math.Max(0, _maxBytesPerWindow - _reservedBytes);
            }
        }
    }

    public bool TryReserve(long bytes)
    {
        if (IsUnlimited)
        {
            return true;
        }

        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Cannot reserve a negative number of bytes.");
        }

        lock (_gate)
        {
            RollWindowIfElapsed(_clock.UtcNow);

            // Compared against what is left rather than as `_reservedBytes + bytes > max`, which
            // overflows to a negative total for reservations near long.MaxValue and would then
            // read as "plenty of room" - re-authorising everything after it. The invariant
            // `_reservedBytes <= _maxBytesPerWindow` makes this subtraction safe.
            if (bytes > _maxBytesPerWindow - _reservedBytes)
            {
                return false;
            }

            _reservedBytes += bytes;
            return true;
        }
    }

    private void RollWindowIfElapsed(DateTimeOffset now)
    {
        if (now - _windowStart < _window)
        {
            return;
        }

        _windowStart = now;
        _reservedBytes = 0;
    }
}
