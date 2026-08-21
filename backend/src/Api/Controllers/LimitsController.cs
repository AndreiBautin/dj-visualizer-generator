using DjVisualizer.Api.Contracts;
using DjVisualizer.Domain.Uploads;
using Microsoft.AspNetCore.Mvc;

namespace DjVisualizer.Api.Controllers;

/// <summary>
/// Publishes the instance's effective upload limits.
/// </summary>
/// <remarks>
/// Exists so the limits have exactly one source of truth. They differ sharply between
/// deployments - a self-hosted instance accepts a 2 GB, six-hour set, while the free-tier demo
/// accepts 60 MB and fifteen minutes - and a frontend that hardcoded either would promise
/// something the server rejects. Nothing here is sensitive: the same numbers are visible to any
/// caller who uploads a file that is one byte too large.
/// </remarks>
[ApiController]
[Route("limits")]
public sealed class LimitsController(UploadLimits limits) : ControllerBase
{
    [HttpGet]
    public ActionResult<UploadLimitsResponse> Get() =>
        new UploadLimitsResponse(limits.MaxAudioBytes, limits.MaxImageBytes, limits.MaxDurationSeconds);
}
