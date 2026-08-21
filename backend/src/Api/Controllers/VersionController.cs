using DjVisualizer.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DjVisualizer.Api.Controllers;

/// <summary>
/// Identifies the build this instance is running, so a deployed page can be tied back to a commit.
/// </summary>
/// <remarks>
/// Resolved at <em>runtime</em> rather than baked into the frontend bundle at build time. The
/// bundle approach needs the commit threaded through as a Docker build argument, which the host
/// has to support and pass - and when it does not, the value silently stays at its "unknown"
/// default and the footer just never appears, which is exactly what happened on the first deploy.
/// Reading an environment variable the platform already sets works on every host and fails
/// visibly (null) rather than silently.
/// </remarks>
[ApiController]
[Route("version")]
public sealed class VersionController(IConfiguration configuration) : ControllerBase
{
    /// <summary>Short commit SHAs are what people actually compare; the full 40 is noise.</summary>
    private const int ShortShaLength = 7;

    [HttpGet]
    public ActionResult<VersionResponse> Get()
    {
        // BUILD_COMMIT first so any host can set it explicitly; RENDER_GIT_COMMIT is the one
        // Render populates by itself, which is what makes this work with no configuration.
        var commit = configuration["BUILD_COMMIT"] ?? configuration["RENDER_GIT_COMMIT"];

        if (string.IsNullOrWhiteSpace(commit))
        {
            return new VersionResponse(null);
        }

        var trimmed = commit.Trim();
        return new VersionResponse(trimmed.Length > ShortShaLength ? trimmed[..ShortShaLength] : trimmed);
    }
}
