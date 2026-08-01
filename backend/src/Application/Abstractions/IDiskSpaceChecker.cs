namespace DjVisualizer.Application.Abstractions;

/// <summary>Reports free space on the volume backing the jobs root, so job creation can be
/// rejected early rather than failing mid-upload or mid-render when the disk fills up.</summary>
public interface IDiskSpaceChecker
{
    long GetAvailableFreeBytes();
}
