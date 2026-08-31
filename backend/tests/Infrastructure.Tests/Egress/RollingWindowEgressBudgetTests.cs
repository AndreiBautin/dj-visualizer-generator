using DjVisualizer.Application.Abstractions;
using DjVisualizer.Infrastructure.Egress;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Egress;

public class RollingWindowEgressBudgetTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    /// <summary>
    /// A settable clock rather than the real one: a budget that resets on a timer could otherwise
    /// only be tested by waiting a day, so it would not be tested at all.
    /// </summary>
    private sealed class MovableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan by) => UtcNow += by;
    }

    [Fact]
    public void TryReserve_Allows_Reservations_Up_To_The_Budget()
    {
        var sut = new RollingWindowEgressBudget(1000, Day, new MovableClock(Start));

        sut.TryReserve(600).Should().BeTrue();
        sut.TryReserve(400).Should().BeTrue();
        sut.RemainingBytes.Should().Be(0);
    }

    /// <summary>
    /// Reserving rather than measuring after the fact is the point: a response bigger than what
    /// is left must be refused before it is streamed, not discovered to have overshot afterwards.
    /// </summary>
    [Fact]
    public void TryReserve_Refuses_A_Reservation_That_Would_Exceed_The_Budget_And_Charges_Nothing()
    {
        var sut = new RollingWindowEgressBudget(1000, Day, new MovableClock(Start));
        sut.TryReserve(900);

        sut.TryReserve(200).Should().BeFalse();

        sut.RemainingBytes.Should().Be(100, "a refused reservation must leave the budget untouched");
        sut.TryReserve(100).Should().BeTrue("what remained is still available to a smaller response");
    }

    [Fact]
    public void TryReserve_Starts_A_Fresh_Budget_Once_The_Window_Has_Elapsed()
    {
        var clock = new MovableClock(Start);
        var sut = new RollingWindowEgressBudget(1000, Day, clock);
        sut.TryReserve(1000);
        sut.TryReserve(1).Should().BeFalse();

        clock.Advance(Day);

        sut.TryReserve(1000).Should().BeTrue();
    }

    [Fact]
    public void TryReserve_Keeps_The_Same_Budget_While_The_Window_Is_Still_Running()
    {
        var clock = new MovableClock(Start);
        var sut = new RollingWindowEgressBudget(1000, Day, clock);
        sut.TryReserve(1000);

        clock.Advance(Day - TimeSpan.FromSeconds(1));

        sut.TryReserve(1).Should().BeFalse();
    }

    /// <summary>
    /// Zero means unlimited, which is the self-hosted default. Getting this backwards would take
    /// a private instance from "bandwidth is not metered here" to "serve nothing at all", and the
    /// symptom - every download failing - would look like a broken renderer rather than a config
    /// default.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void TryReserve_Always_Succeeds_When_No_Budget_Is_Configured(long configured)
    {
        var sut = new RollingWindowEgressBudget(configured, Day, new MovableClock(Start));

        sut.TryReserve(long.MaxValue / 2).Should().BeTrue();
        sut.TryReserve(long.MaxValue / 2).Should().BeTrue();
        sut.RemainingBytes.Should().Be(long.MaxValue);
    }

    /// <summary>
    /// Two large reservations must not wrap past <see cref="long.MaxValue"/> into a negative
    /// total, which would silently re-authorise everything after it.
    /// </summary>
    [Fact]
    public void TryReserve_Does_Not_Overflow_On_Reservations_Near_The_Long_Ceiling()
    {
        var sut = new RollingWindowEgressBudget(long.MaxValue, Day, new MovableClock(Start));

        sut.TryReserve(long.MaxValue - 10).Should().BeTrue();
        sut.TryReserve(long.MaxValue - 10).Should().BeFalse();
    }

    [Fact]
    public void Constructor_Rejects_A_Non_Positive_Window()
    {
        var construct = () => new RollingWindowEgressBudget(1000, TimeSpan.Zero, new MovableClock(Start));

        construct.Should().Throw<ArgumentOutOfRangeException>();
    }
}
