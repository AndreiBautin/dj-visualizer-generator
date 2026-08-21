using System.Net;
using System.Net.Http.Json;
using DjVisualizer.Api.IntegrationTests.Support;
using FluentAssertions;

namespace DjVisualizer.Api.IntegrationTests;

/// <summary>
/// The footer shows the commit an instance is running so a deployed page can be tied back to a
/// build. Resolved at runtime rather than compiled into the bundle: the build-time route needs
/// the host to pass a Docker build argument, and when it does not the value silently stays at its
/// "unknown" default and the footer never appears - which is precisely what happened on the first
/// real deploy.
/// </summary>
public class VersionEndpointTests
{
    private static JobsApiFactory FactoryWith(Dictionary<string, string?> settings) =>
        new() { ExtraConfiguration = settings };

    [Fact]
    public async Task Reports_The_Commit_Render_Sets_Without_Any_Configuration()
    {
        using var factory = FactoryWith(new() { ["RENDER_GIT_COMMIT"] = "7914ae3f1c2d4b5a6e7f8091a2b3c4d5e6f70819" });
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<VersionBody>("/version");

        body!.Commit.Should().Be("7914ae3", "a short sha is what people actually compare");
    }

    [Fact]
    public async Task An_Explicit_Build_Commit_Wins_So_Any_Host_Can_Set_It()
    {
        using var factory = FactoryWith(new()
        {
            ["BUILD_COMMIT"] = "abcdef1234567",
            ["RENDER_GIT_COMMIT"] = "9999999999999",
        });
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<VersionBody>("/version");

        body!.Commit.Should().Be("abcdef1");
    }

    /// <summary>
    /// Null rather than a placeholder string: the footer keys off the absence, and inventing
    /// something like "unknown" would put a meaningless value on the page.
    /// </summary>
    [Fact]
    public async Task Reports_Null_When_The_Host_Exposes_No_Commit()
    {
        using var factory = FactoryWith(new() { ["BUILD_COMMIT"] = "", ["RENDER_GIT_COMMIT"] = "" });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<VersionBody>())!.Commit.Should().BeNull();
    }

    private sealed record VersionBody(string? Commit);
}
