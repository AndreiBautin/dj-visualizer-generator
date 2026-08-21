using DjVisualizer.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DjVisualizer.Api.IntegrationTests.Support;

public sealed class JobsApiFactory : WebApplicationFactory<Program>
{
    public string JobsRootPath { get; } = Path.Combine(Path.GetTempPath(), "djvisualizer-api-tests", Guid.NewGuid().ToString("N"));

    public TimeSpan StubAudioDuration { get; set; } = TimeSpan.FromMinutes(45);

    /// <summary>Mirrors the <c>Demo:Enabled</c> switch. Left off by default so the default-off
    /// behaviour is what most tests exercise.</summary>
    public bool DemoEnabled { get; init; }

    /// <summary>Absolute paths to stand in for the bundled sample assets. Left null to simulate
    /// a deployment where <c>Demo:Enabled</c> is on but the assets never made it into the image.
    /// </summary>
    public string? DemoAudioFilePath { get; init; }

    public string? DemoArtworkFilePath { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jobs:RootPath"] = JobsRootPath,
                ["Jobs:MaxAudioBytes"] = "10000000",
                ["Jobs:MaxImageBytes"] = "5000000",
                ["Jobs:MaxDurationSeconds"] = "21600",
                ["Demo:Enabled"] = DemoEnabled ? "true" : "false",
                ["Demo:AudioFilePath"] = DemoAudioFilePath ?? "demo/missing-sample.mp3",
                ["Demo:ArtworkFilePath"] = DemoArtworkFilePath ?? "demo/missing-sample.png",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAudioProbe>();
            services.AddSingleton<IAudioProbe>(new StubAudioProbe(() => StubAudioDuration));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(JobsRootPath))
        {
            Directory.Delete(JobsRootPath, recursive: true);
        }
    }
}
