namespace DjVisualizer.Infrastructure.Jobs;

/// <summary>Exclusive OS handle: released even after a crash; the marker file may remain.</summary>
public sealed class FileSystemInstanceLease : IDisposable
{
    private readonly FileStream _handle;
    public FileSystemInstanceLease(string root, string role)
    {
        Directory.CreateDirectory(root);
        _handle = new FileStream(Path.Combine(root, $".{role}.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }
    public void Dispose() => _handle.Dispose();
}
