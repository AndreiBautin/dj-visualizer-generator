using DjVisualizer.Infrastructure.Rendering;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Rendering;

/// <summary>
/// The render runs four ffmpeg passes but only the last two can report progress from inside
/// themselves. Mapping each pass onto a slice of the overall bar is what stops the UI sitting at
/// 0% while the first passes run - on a constrained host that silence lasted about two thirds of
/// the whole render and read as "hung".
/// </summary>
public class RenderProgressScaleTests
{
    [Fact]
    public void The_Phases_Cover_The_Bar_In_Order_Without_Gaps_Or_Overlap()
    {
        RenderProgressScale.StaticVinylComplete.Should().BeLessThan(RenderProgressScale.AmbientBackgroundComplete);
        RenderProgressScale.AmbientBackgroundComplete.Should().Be(RenderProgressScale.LoopSegmentStart);
        RenderProgressScale.LoopSegmentEnd.Should().Be(RenderProgressScale.MuxStart);
        RenderProgressScale.MuxStart.Should().BeLessThan(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void The_Loop_Segment_Pass_Maps_Into_Its_Own_Slice(int passPercent)
    {
        var mapped = RenderProgressScale.ForLoopSegment(passPercent);

        mapped.Should().BeInRange(RenderProgressScale.LoopSegmentStart, RenderProgressScale.LoopSegmentEnd);
    }

    [Fact]
    public void The_Loop_Segment_Pass_Spans_Its_Slice_End_To_End()
    {
        RenderProgressScale.ForLoopSegment(0).Should().Be(RenderProgressScale.LoopSegmentStart);
        RenderProgressScale.ForLoopSegment(100).Should().Be(RenderProgressScale.LoopSegmentEnd);
    }

    [Fact]
    public void The_Mux_Pass_Finishes_The_Bar()
    {
        RenderProgressScale.ForMux(0).Should().Be(RenderProgressScale.MuxStart);
        RenderProgressScale.ForMux(100).Should().Be(100);
    }

    /// <summary>A pass reporting nonsense must not drive the bar outside its slice - Job.
    /// UpdateProgress throws outside 0-100, which would fail an otherwise fine render.</summary>
    [Theory]
    [InlineData(-50)]
    [InlineData(1000)]
    public void Out_Of_Range_Pass_Progress_Is_Clamped_Rather_Than_Propagated(int passPercent)
    {
        RenderProgressScale.ForLoopSegment(passPercent).Should().BeInRange(0, 100);
        RenderProgressScale.ForMux(passPercent).Should().BeInRange(0, 100);
    }

    [Fact]
    public void Mapping_Is_Monotonic_Within_A_Pass()
    {
        var previous = -1;
        for (var passPercent = 0; passPercent <= 100; passPercent++)
        {
            var mapped = RenderProgressScale.ForMux(passPercent);
            mapped.Should().BeGreaterThanOrEqualTo(previous);
            previous = mapped;
        }
    }
}
