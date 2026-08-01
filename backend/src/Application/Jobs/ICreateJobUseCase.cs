using DjVisualizer.Application.Common;

namespace DjVisualizer.Application.Jobs;

public interface ICreateJobUseCase
{
    Task<Result<CreateJobResult>> ExecuteAsync(CreateJobRequest request, CancellationToken cancellationToken);
}
