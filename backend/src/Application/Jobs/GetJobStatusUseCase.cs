using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Common;
using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Jobs;

public sealed class GetJobStatusUseCase(IJobRepository jobRepository) : IGetJobStatusUseCase
{
    public async Task<Result<JobStatusResult>> ExecuteAsync(string jobId, CancellationToken cancellationToken)
    {
        JobId id;
        try
        {
            id = JobId.Parse(jobId);
        }
        catch (InvalidJobIdException)
        {
            return Result<JobStatusResult>.Failure(Error.NotFound("No job exists for the given id."));
        }

        var job = await jobRepository.FindAsync(id, cancellationToken);
        if (job is null)
        {
            return Result<JobStatusResult>.Failure(Error.NotFound("No job exists for the given id."));
        }

        return Result<JobStatusResult>.Success(new JobStatusResult(
            job.Id.ToString(),
            job.Status.ToString(),
            job.Progress,
            job.ErrorMessage));
    }
}
