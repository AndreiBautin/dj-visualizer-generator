using DjVisualizer.Infrastructure.Time;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Time;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_Returns_The_Current_Time()
    {
        var sut = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var value = sut.UtcNow;
        var after = DateTimeOffset.UtcNow;

        value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
