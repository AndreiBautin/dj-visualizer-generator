using System.Net;
using System.Net.Http.Json;
using DjVisualizer.Api.IntegrationTests.Support;
using FluentAssertions;

namespace DjVisualizer.Api.IntegrationTests;

/// <summary>
/// Covers POST /jobs/sample, the one-click path a first-time visitor takes. The endpoint reads
/// files from disk on the server's behalf, so its failure modes matter more than its happy path:
/// it must stay switched off unless a deployment opts in, and it must never report which server
/// paths it probed.
/// </summary>
public class SampleJobEndpointTests : IDisposable
{
    private static readonly byte[] ValidMp3Bytes =
        [0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04];

    private static readonly byte[] ValidPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

    private readonly string _assetDirectory = Path.Combine(Path.GetTempPath(), $"djvisualizer-demo-{Guid.NewGuid():N}");
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_assetDirectory))
        {
            Directory.Delete(_assetDirectory, recursive: true);
        }
    }

    private (string AudioPath, string ArtworkPath) WriteSampleAssets()
    {
        Directory.CreateDirectory(_assetDirectory);
        var audioPath = Path.Combine(_assetDirectory, "sample-mix.mp3");
        var artworkPath = Path.Combine(_assetDirectory, "sample-artwork.png");
        File.WriteAllBytes(audioPath, ValidMp3Bytes);
        File.WriteAllBytes(artworkPath, ValidPngBytes);
        return (audioPath, artworkPath);
    }

    private HttpClient CreateClient(JobsApiFactory factory)
    {
        _disposables.Add(factory);
        var client = factory.CreateClient();
        _disposables.Add(client);
        return client;
    }

    [Fact]
    public async Task Returns_404_When_The_Demo_Is_Not_Enabled()
    {
        var client = CreateClient(new JobsApiFactory());

        var response = await client.PostAsJsonAsync("/jobs/sample", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creates_A_Job_From_The_Bundled_Assets_When_Enabled()
    {
        var (audioPath, artworkPath) = WriteSampleAssets();
        var client = CreateClient(new JobsApiFactory
        {
            DemoEnabled = true,
            DemoAudioFilePath = audioPath,
            DemoArtworkFilePath = artworkPath,
        });

        var response = await client.PostAsJsonAsync("/jobs/sample", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateJobResponseBody>();
        body!.JobId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParseExact(body.JobId, "D", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Honours_Render_Settings_Supplied_With_The_Sample_Request()
    {
        var (audioPath, artworkPath) = WriteSampleAssets();
        var client = CreateClient(new JobsApiFactory
        {
            DemoEnabled = true,
            DemoAudioFilePath = audioPath,
            DemoArtworkFilePath = artworkPath,
        });

        var response = await client.PostAsJsonAsync("/jobs/sample", new
        {
            title = "Custom Sample Title",
            preset = "1080p",
            rotationSpeedSeconds = 4.0,
            captionFont = "mono-bold",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rejects_Render_Settings_That_Fail_Domain_Validation()
    {
        var (audioPath, artworkPath) = WriteSampleAssets();
        var client = CreateClient(new JobsApiFactory
        {
            DemoEnabled = true,
            DemoAudioFilePath = audioPath,
            DemoArtworkFilePath = artworkPath,
        });

        var response = await client.PostAsJsonAsync("/jobs/sample", new { preset = "4k" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// If the assets are missing this is a deployment fault, not a caller fault - so it must be a
    /// 503 rather than a 404, and the body must not disclose the server paths that were probed.
    /// </summary>
    [Fact]
    public async Task Returns_503_Without_Disclosing_Server_Paths_When_The_Assets_Are_Missing()
    {
        var client = CreateClient(new JobsApiFactory
        {
            DemoEnabled = true,
            DemoAudioFilePath = Path.Combine(_assetDirectory, "nope.mp3"),
            DemoArtworkFilePath = Path.Combine(_assetDirectory, "nope.png"),
        });

        var response = await client.PostAsJsonAsync("/jobs/sample", new { });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(_assetDirectory);
        body.Should().NotContainAny("nope.mp3", "nope.png", Path.GetTempPath());
    }

    private sealed record CreateJobResponseBody(string JobId);
}
