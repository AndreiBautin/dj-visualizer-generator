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
    private static readonly int End = RenderProgressScale.LoopSegmentEndFor(TimeSpan.FromMinutes(5));

    [Fact]
    public void The_Phases_Cover_The_Bar_In_Order_Without_Gaps_Or_Overlap()
    {
        RenderProgressScale.StaticVinylComplete.Should().BeLessThan(RenderProgressScale.BackgroundComplete);
        RenderProgressScale.BackgroundComplete.Should().Be(RenderProgressScale.LoopSegmentStart);
        End.Should().BeInRange(RenderProgressScale.LoopSegmentStart + 1, 99);
    }

    /// <summary>The live 40-minute render spent about 19% of its time in the rotation pass; the
    /// bar should hand over near there, not at the old fixed 45%.</summary>
    [Fact]
    public void A_Long_Mix_Hands_The_Bar_To_The_Mux_Early_And_A_Short_One_Late()
    {
        RenderProgressScale.LoopSegmentEndFor(TimeSpan.FromMinutes(40)).Should().BeInRange(20, 30);
        RenderProgressScale.LoopSegmentEndFor(TimeSpan.FromSeconds(24)).Should().Be(90);
        RenderProgressScale.LoopSegmentEndFor(TimeSpan.FromHours(6)).Should().Be(20, "each pass keeps a visible slice");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void The_Loop_Segment_Pass_Maps_Into_Its_Own_Slice(int passPercent) =>
        RenderProgressScale.ForLoopSegment(passPercent, End).Should().BeInRange(RenderProgressScale.LoopSegmentStart, End);

    [Fact]
    public void The_Passes_Span_Their_Slices_End_To_End()
    {
        RenderProgressScale.ForLoopSegment(0, End).Should().Be(RenderProgressScale.LoopSegmentStart);
        RenderProgressScale.ForLoopSegment(100, End).Should().Be(End);
        RenderProgressScale.ForMux(0, End).Should().Be(End);
        RenderProgressScale.ForMux(100, End).Should().Be(100);
    }

    /// <summary>A pass reporting nonsense must not drive the bar outside its slice - Job.
    /// UpdateProgress throws outside 0-100, which would fail an otherwise fine render.</summary>
    [Theory]
    [InlineData(-50)]
    [InlineData(1000)]
    public void Out_Of_Range_Pass_Progress_Is_Clamped_Rather_Than_Propagated(int passPercent)
    {
        RenderProgressScale.ForLoopSegment(passPercent, End).Should().BeInRange(0, 100);
        RenderProgressScale.ForMux(passPercent, End).Should().BeInRange(0, 100);
    }

    [Fact]
    public void Mapping_Is_Monotonic_Within_A_Pass()
    {
        var previous = -1;
        for (var passPercent = 0; passPercent <= 100; passPercent++)
        {
            var mapped = RenderProgressScale.ForMux(passPercent, End);
            mapped.Should().BeGreaterThanOrEqualTo(previous);
            previous = mapped;
        }
    }
}
