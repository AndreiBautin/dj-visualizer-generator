using System.Net;
using DjVisualizer.Api.RateLimiting;
using FluentAssertions;

namespace DjVisualizer.Api.IntegrationTests;

public class ClientPartitionTests
{
    [Fact]
    public void Addresses_In_One_Ipv6_Slash_64_Share_A_Partition() =>
        ClientPartition.For(IPAddress.Parse("2001:db8:1:2::1"))
            .Should().Be(ClientPartition.For(IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:ffff")));

    [Fact]
    public void Different_Ipv6_Slash_64s_Do_Not() =>
        ClientPartition.For(IPAddress.Parse("2001:db8:1:2::1"))
            .Should().NotBe(ClientPartition.For(IPAddress.Parse("2001:db8:1:3::1")));

    [Fact]
    public void Ipv4_Is_Used_As_Is_Even_When_Mapped_Into_Ipv6()
    {
        ClientPartition.For(IPAddress.Parse("203.0.113.7")).Should().Be("203.0.113.7");
        ClientPartition.For(IPAddress.Parse("::ffff:203.0.113.7")).Should().Be("203.0.113.7");
    }

    [Fact]
    public void A_Missing_Address_Has_A_Stable_Partition() =>
        ClientPartition.For(null).Should().Be("unknown");
}
