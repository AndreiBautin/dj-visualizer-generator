using DjVisualizer.Api.Contracts;
using DjVisualizer.Application.Common;
using DjVisualizer.Application.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
