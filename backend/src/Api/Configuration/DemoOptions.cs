namespace DjVisualizer.Api.Configuration;

/// <summary>
/// The bundled sample mix and artwork offered by <c>POST /jobs/sample</c>, so a first-time
/// visitor can watch the pipeline run without owning a DJ set or waiting on a large upload.
/// </summary>
/// <remarks>
/// Both files are synthesized by <c>scripts/generate-demo-assets.sh</c> and committed to the
/// repository - there is no path by which a real recording could take their place on a deployed
/// instance. See docs/DEMO_DATA.md.
/// </remarks>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Off unless a deployment turns it on, so the sample endpoint is not silently
    /// present in a private or self-hosted instance that never asked for it.</summary>
    public bool Enabled { get; set; }

    /// <summary>Relative paths are resolved against the application's content root.</summary>
    public string AudioFilePath { get; set; } = "demo/sample-mix.mp3";

    public string ArtworkFilePath { get; set; } = "demo/sample-artwork.png";

    public string Title { get; set; } = "Sample Demo Mix";
}
