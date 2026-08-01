using DjVisualizer.Domain.Jobs;

namespace DjVisualizer.Application.Jobs;

public interface IProcessRenderJobUseCase
{
    Task ExecuteAsync(Job job, CancellationToken cancellationToken);
}
