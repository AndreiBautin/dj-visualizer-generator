using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Uploads;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Uploads;

public class UploadLimitsTests
{
    [Fact]
    public void Create_Accepts_Positive_Limits()
    {
        var limits = new UploadLimits(
            maxAudioBytes: 2_147_483_648,
            maxImageBytes: 26_214_400,
            maxDurationSeconds: 21_600,
            minFreeDiskBytes: 3_221_225_472);

        limits.MaxAudioBytes.Should().Be(2_147_483_648);
        limits.MaxImageBytes.Should().Be(26_214_400);
        limits.MaxDurationSeconds.Should().Be(21_600);
        limits.MinFreeDiskBytes.Should().Be(3_221_225_472);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(-1, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, -1, 1)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 1, 1, -1)]
    public void Create_Rejects_NonPositive_Limits(long maxAudioBytes, long maxImageBytes, int maxDurationSeconds, long minFreeDiskBytes)
    {
        var act = () => new UploadLimits(maxAudioBytes, maxImageBytes, maxDurationSeconds, minFreeDiskBytes);

        act.Should().Throw<InvalidUploadLimitsException>();
    }
}
