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

    /// <summary>Extra configuration entries layered on last, for settings that have no dedicated
    /// property here - platform-supplied variables like RENDER_GIT_COMMIT, for instance.</summary>
    public Dictionary<string, string?> ExtraConfiguration { get; init; } = [];

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

            if (ExtraConfiguration.Count > 0)
            {
                config.AddInMemoryCollection(ExtraConfiguration);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAudioProbe>();
            services.AddSingleton<IAudioProbe>(new StubAudioProbe(() => StubAudioDuration));
        });
    }

    private bool _disposing;

    /// <summary>
    /// The base <c>Dispose(true)</c> calls <c>DisposeAsync</c>, which calls back into this method
    /// <em>before</em> it disposes the host - so the nested call ran while the API still held
    /// <c>.api.lock</c> open and the delete failed on Windows. Only the outermost call cleans up,
    /// once the host is gone.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposing)
        {
            return;
        }

        _disposing = true;
        base.Dispose(disposing);

        // The host can let go of its file handles a moment after it reports disposed - on Windows
        // a delete in that window fails with a sharing violation, which failed the pre-push gate
        // about one push in two. A short retry waits it out; a real leak still fails after it.
        for (var attempt = 1; Directory.Exists(JobsRootPath); attempt++)
        {
            try
            {
                Directory.Delete(JobsRootPath, recursive: true);
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }
}
