using DjVisualizer.Api.Middleware;
using FluentAssertions;

namespace DjVisualizer.Api.IntegrationTests;

public class StaticAssetCachingTests
{
    [Theory]
    [InlineData("/assets/index-Cg7Qyif0.js")]
    [InlineData("/assets/geist-latin-wght-normal-BgDaEnEv.woff2")]
    public void Fingerprinted_Assets_Are_Cached_For_Good(string path) =>
        StaticAssetCaching.CacheControlFor(path).Should().Be(StaticAssetCaching.Immutable);

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/sw.js")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/fonts/Poppins-ExtraBold.latin.woff2")]
    public void Anything_With_A_Stable_Name_Revalidates_So_Deploys_Reach_Returning_Visitors(string path) =>
        StaticAssetCaching.CacheControlFor(path).Should().Be(StaticAssetCaching.Revalidate);
}
