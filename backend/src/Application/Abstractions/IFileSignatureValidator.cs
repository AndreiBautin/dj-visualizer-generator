namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// Validates that a file's extension AND its magic-byte signature both match one of the
/// formats allowed for the given category, so a renamed malicious file can't slip through
/// on extension alone.
/// </summary>
public interface IFileSignatureValidator
{
    bool IsValid(FileCategory category, string fileExtension, byte[] header);
}
