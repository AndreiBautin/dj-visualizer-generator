using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Rendering;
using DjVisualizer.Infrastructure.Uploads;
using DjVisualizer.Worker.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DjVisualizer.Worker;

/// <summary>
/// The render worker's own registrations, factored out of <c>Program.cs</c> so the same wiring
/// serves both composition roots: the standalone Worker process (docker-compose, where rendering
/// scales independently of the API) and the API process itself (single-container hosting, where
/// only one process is available). The hosted services, the use case and every dependency they
/// touch are identical in both - only where they are constructed differs.
/// </summary>
/// <remarks>
/// Deliberately does <em>not</em> register the shared job-storage services (<see cref="IClock"/>,
/// <c>FileSystemJobStore</c>, <c>UploadLimits</c>, <see cref="IAudioProbe"/>,
/// <see cref="IJobFileStorage"/>): the API composition root already registers those for its own
/// use, and registering them twice would create a second <c>FileSystemJobStore</c> instance
/// pointing at the same directory. Callers must have registered them first.
/// </remarks>
public static class WorkerServiceRegistration
{
    /// <param name="resolveJobsRootPath">Deferred so it runs after the host is built, when every
    /// configuration source has been merged - matching how both composition roots resolve the
    /// jobs root for their other services.</param>
    public static IServiceCollection AddRenderWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<string> resolveJobsRootPath)
    {
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));

        services.AddSingleton<IJobInputFileLocator>(_ => new FileSystemJobInputFileLocator(resolveJobsRootPath()));
        services.AddSingleton<IVideoRenderer>(sp =>
        {
            var workerOptions = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            var fontFilePaths = new Dictionary<CaptionFont, string>
            {
                [CaptionFont.SansBold] = workerOptions.FontFilePathSansBold,
                [CaptionFont.SerifBold] = workerOptions.FontFilePathSerifBold,
                [CaptionFont.MonoBold] = workerOptions.FontFilePathMonoBold,
            };
            return new FfmpegVideoRenderer(fontFilePaths, workerOptions.VideoCodec, workerOptions.X264Preset);
        });

        // Singleton, not scoped: JobPollingService is itself a singleton hosted service and
        // consumes this directly (no per-request scope exists in a Worker), and all of its own
        // dependencies are singletons too, so this is safe - it holds no per-job mutable state.
        services.AddSingleton<IProcessRenderJobUseCase, ProcessRenderJobUseCase>();

        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(_ => new InstanceLeaseService(resolveJobsRootPath(), "worker"));
        services.AddHostedService<JobPollingService>();
        services.AddHostedService<CleanupService>();

        return services;
    }
}
