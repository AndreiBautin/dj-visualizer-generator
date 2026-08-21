using Xunit;

namespace DjVisualizer.Infrastructure.Tests.Support;

/// <summary>The <see cref="RequiresFfmpegFactAttribute"/> counterpart for data-driven tests.</summary>
public sealed class RequiresFfmpegTheoryAttribute : TheoryAttribute
{
    public RequiresFfmpegTheoryAttribute()
    {
        if (!FfmpegAvailability.IsAvailable)
        {
            Skip = "ffprobe is not installed on this machine.";
        }
    }
}
