using DjVisualizer.Application.Jobs;
using FluentAssertions;

namespace DjVisualizer.Application.Tests.Jobs;

public class UploadDiskReservationsTests
{
    /// <summary>Two uploads that each fit the free space alone, but not together.</summary>
    [Fact]
    public void A_Second_Concurrent_Upload_Is_Refused_When_Both_Would_Breach_The_Floor()
    {
        var sut = new UploadDiskReservations();

        using var first = sut.TryReserve(freeBytes: 1000, minFreeBytes: 500, worstCaseBytes: 600);
        var second = sut.TryReserve(freeBytes: 1000, minFreeBytes: 500, worstCaseBytes: 600);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public void A_Lone_Upload_Needs_Only_The_Floor_Because_The_Floor_Already_Covers_One() =>
        new UploadDiskReservations().TryReserve(freeBytes: 500, minFreeBytes: 500, worstCaseBytes: 600).Should().NotBeNull();

    [Fact]
    public void Finishing_An_Upload_Gives_Its_Space_Back_Once()
    {
        var sut = new UploadDiskReservations();
        var first = sut.TryReserve(1000, 500, 600)!;

        first.Dispose();
        first.Dispose(); // a double release must not mint space

        using var a = sut.TryReserve(1000, 500, 600);
        var b = sut.TryReserve(1000, 500, 600);
        a.Should().NotBeNull();
        b.Should().BeNull();
    }
}
