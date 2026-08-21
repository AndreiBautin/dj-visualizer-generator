using DjVisualizer.Api.Configuration;
using DjVisualizer.Api.Contracts;
using DjVisualizer.Application.Common;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DjVisualizer.Api.Controllers;

[ApiController]
[Route("jobs")]
public sealed class JobsController(
    ICreateJobUseCase createJobUseCase,
    IGetJobStatusUseCase getJobStatusUseCase,
    IGetJobDownloadUseCase getJobDownloadUseCase) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("job-creation")]
    public async Task<IActionResult> Create([FromForm] CreateJobFormRequest request, CancellationToken cancellationToken)
    {
        if (request.Audio is null)
        {
            return BadRequestProblem("An audio file is required.");
        }

        if (request.Artwork is null)
        {
            return BadRequestProblem("An artwork file is required.");
        }

        await using var audioStream = request.Audio.OpenReadStream();
        await using var artworkStream = request.Artwork.OpenReadStream();

        var result = await createJobUseCase.ExecuteAsync(
            new CreateJobRequest(
                request.Title ?? "",
                request.Preset ?? "",
                audioStream,
                request.Audio.FileName,
                artworkStream,
                request.Artwork.FileName,
                request.RotationSpeedSeconds,
                request.CaptionFont),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var response = new CreateJobResponse(result.Value!.JobId.ToString());
        return CreatedAtAction(nameof(GetStatus), new { jobId = response.JobId }, response);
    }

    /// <summary>
    /// Creates a job from the bundled sample mix and artwork, so a visitor can see the render
    /// pipeline run without supplying their own files. Goes through exactly the same use case,
    /// validation and queue as an uploaded job - the only difference is where the two streams
    /// come from, which is what makes it worth demonstrating rather than a separate shortcut.
    /// </summary>
    [HttpPost("sample")]
    [EnableRateLimiting("job-creation")]
    public async Task<IActionResult> CreateFromSample(
        [FromBody] CreateSampleJobRequest? request,
        [FromServices] IOptions<DemoOptions> demoOptions,
        [FromServices] IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var demo = demoOptions.Value;
        if (!demo.Enabled)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = "The sample mix is not enabled on this instance.",
            });
        }

        var audioPath = ResolveContentPath(environment, demo.AudioFilePath);
        var artworkPath = ResolveContentPath(environment, demo.ArtworkFilePath);
        if (!System.IO.File.Exists(audioPath) || !System.IO.File.Exists(artworkPath))
        {
            // A deployment misconfiguration, not a caller error - and the caller must not be told
            // which server paths were probed.
            return new ObjectResult(new ProblemDetails
            {
                Title = "Service Unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "The sample mix is unavailable on this instance.",
            })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }

        await using var audioStream = System.IO.File.OpenRead(audioPath);
        await using var artworkStream = System.IO.File.OpenRead(artworkPath);

        var result = await createJobUseCase.ExecuteAsync(
            new CreateJobRequest(
                string.IsNullOrWhiteSpace(request?.Title) ? demo.Title : request.Title,
                request?.Preset ?? VideoPreset.Hd720p.Name,
                audioStream,
                Path.GetFileName(audioPath),
                artworkStream,
                Path.GetFileName(artworkPath),
                request?.RotationSpeedSeconds,
                request?.CaptionFont),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var response = new CreateJobResponse(result.Value!.JobId.ToString());
        return CreatedAtAction(nameof(GetStatus), new { jobId = response.JobId }, response);
    }

    private static string ResolveContentPath(IHostEnvironment environment, string configuredPath) =>
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetStatus(string jobId, CancellationToken cancellationToken)
    {
        var result = await getJobStatusUseCase.ExecuteAsync(jobId, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("{jobId}/download")]
    public async Task<IActionResult> Download(string jobId, CancellationToken cancellationToken)
    {
        var result = await getJobDownloadUseCase.ExecuteAsync(jobId, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var stream = System.IO.File.OpenRead(result.Value!.FilePath);
        return File(stream, "video/mp4", result.Value.FileName);
    }

    private IActionResult MapError(Error error) => error.Code switch
    {
        ErrorCodes.Validation => BadRequestProblem(error.Message),
        ErrorCodes.NotFound => NotFound(new ProblemDetails
        {
            Title = "Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = error.Message,
        }),
        ErrorCodes.NotReady => new ObjectResult(new ProblemDetails
        {
            Title = "Not Ready",
            Status = StatusCodes.Status409Conflict,
            Detail = error.Message,
        })
        { StatusCode = StatusCodes.Status409Conflict },
        ErrorCodes.Unavailable => new ObjectResult(new ProblemDetails
        {
            Title = "Service Unavailable",
            Status = StatusCodes.Status503ServiceUnavailable,
            Detail = error.Message,
        })
        { StatusCode = StatusCodes.Status503ServiceUnavailable },
        _ => Problem(title: "Request Failed", detail: error.Message, statusCode: StatusCodes.Status502BadGateway),
    };

    private ObjectResult BadRequestProblem(string detail) => new(new ProblemDetails
    {
        Title = "Validation Failed",
        Status = StatusCodes.Status400BadRequest,
        Detail = detail,
    })
    {
        StatusCode = StatusCodes.Status400BadRequest,
    };
}
