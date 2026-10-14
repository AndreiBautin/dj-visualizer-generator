namespace DjVisualizer.Application.Jobs;

/// <summary>
/// Disk space promised to uploads that are still arriving. The free-disk check runs once, before
/// any bytes are read, so without this ten concurrent 500 MB uploads all passed it against the
/// same free space and then wrote 5 GB between them. Each admitted upload holds its worst case
/// (the largest audio plus the largest artwork) until it finishes, and the next one is admitted
/// only if free space still covers the floor after everything already promised. The floor itself
/// is sized to cover one upload, so an upload is checked against the others in flight, not
/// against itself - a lone upload is admitted exactly as before.
/// </summary>
public sealed class UploadDiskReservations
{
    private readonly object _lock = new();
    private long _reservedBytes;

    /// <summary>Null when the upload would push promised space past the floor.</summary>
    public IDisposable? TryReserve(long freeBytes, long minFreeBytes, long worstCaseBytes)
    {
        lock (_lock)
        {
            if (freeBytes - _reservedBytes < minFreeBytes)
            {
                return null;
            }

            _reservedBytes += worstCaseBytes;
        }

        return new Reservation(this, worstCaseBytes);
    }

    private void Release(long bytes)
    {
        lock (_lock)
        {
            _reservedBytes -= bytes;
        }
    }

    private sealed class Reservation(UploadDiskReservations owner, long bytes) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Release(bytes);
            }
        }
    }
}
