namespace DjVisualizer.Api.Middleware;

/// <summary>
/// Cache-Control for the built SPA when this process serves it (single-container hosting).
/// Vite fingerprints everything under <c>/assets/</c>, so a changed file has a new name and the
/// old one can be cached for good. The document and the service worker must revalidate on every
/// load, or a deploy would not reach returning visitors until their cache happened to expire.
/// </summary>
public static class StaticAssetCaching
{
    public const string Immutable = "public, max-age=31536000, immutable";
    public const string Revalidate = "no-cache";

    /// <summary>The Cache-Control value for a request path under the SPA.</summary>
    public static string CacheControlFor(string path)
    {
        if (path.StartsWith("/assets/", StringComparison.Ordinal))
        {
            return Immutable;
        }

        // Fonts and icons keep stable names across deploys, so they revalidate (cheap with ETags)
        // rather than risk a stale copy; only fingerprinted files are safe to pin.
        return Revalidate;
    }
}
