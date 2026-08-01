using DjVisualizer.Infrastructure.Rendering;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

public class FfmpegProgressParserTests
{
    [Fact]
    public void TryParseElapsed_Reads_Microseconds_From_An_Out_Time_Us_Line()
    {
        var success = FfmpegProgressParser.TryParseElapsed("out_time_us=4000000", out var elapsed);

        success.Should().BeTrue();
        elapsed.Should().Be(TimeSpan.FromSeconds(4));
    }

    [Theory]
    [InlineData("frame=120")]
    [InlineData("fps=30.00")]
    [InlineData("out_time_us=not-a-number")]
    [InlineData("")]
    public void TryParseElapsed_Returns_False_For_Non_Elapsed_Lines(string line)
    {
        var success = FfmpegProgressParser.TryParseElapsed(line, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void IsEndOfProgress_Recognizes_The_Terminal_Progress_Line()
    {
        FfmpegProgressParser.IsEndOfProgress("progress=end").Should().BeTrue();
        FfmpegProgressParser.IsEndOfProgress("progress=continue").Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(100, 100, 100)]
    [InlineData(150, 100, 100)]
    public void CalculatePercent_Clamps_To_0_100(double elapsedSeconds, double totalSeconds, int expectedPercent)
    {
        var percent = FfmpegProgressParser.CalculatePercent(TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromSeconds(totalSeconds));

        percent.Should().Be(expectedPercent);
    }

    [Fact]
    public void CalculatePercent_Returns_Zero_For_A_Zero_Or_Negative_Total_Duration()
    {
        FfmpegProgressParser.CalculatePercent(TimeSpan.FromSeconds(5), TimeSpan.Zero).Should().Be(0);
    }
}
