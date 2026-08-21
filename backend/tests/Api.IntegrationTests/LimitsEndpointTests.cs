using System.Net;
using System.Net.Http.Json;
using DjVisualizer.Api.IntegrationTests.Support;
using FluentAssertions;

namespace DjVisualizer.Api.IntegrationTests;

/// <summary>
/// The UI states this instance's limits in its upload hints and pre-checks files against them, so
/// this endpoint is the single source of truth for numbers that differ by an order of magnitude
/// between a self-hosted instance and the free-tier demo.
/// </summary>
public class LimitsEndpointTests : IDisposable
{
    private readonly JobsApiFactory _factory = new();
    private readonly HttpClient _client;

    public LimitsEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Reports_The_Limits_This_Instance_Is_Actually_Configured_With()
    {
        var response = await _client.GetAsync("/limits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadLimitsBody>();

        // Exactly what JobsApiFactory configures - proving the endpoint reflects configuration
        // rather than echoing the compiled-in defaults.
        body!.MaxAudioBytes.Should().Be(10_000_000);
        body.MaxImageBytes.Should().Be(5_000_000);
        body.MaxDurationSeconds.Should().Be(21_600);
    }

    [Fact]
    public async Task Is_Readable_Without_Any_Credentials_And_Carries_The_Api_Security_Headers()
    {
        var response = await _client.GetAsync("/limits");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("default-src 'none'");
    }

    private sealed record UploadLimitsBody(long MaxAudioBytes, long MaxImageBytes, int MaxDurationSeconds);
}
