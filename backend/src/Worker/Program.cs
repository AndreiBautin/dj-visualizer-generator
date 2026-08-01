using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Jobs;
using DjVisualizer.Infrastructure.Rendering;
using DjVisualizer.Infrastructure.Time;
using DjVisualizer.Infrastructure.Uploads;
using DjVisualizer.Worker;
using DjVisualizer.Worker.Configuration;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

// Deferred (factory-based) config reads: these run after the host is fully built, so every
// configuration source has been merged by the time they execute - mirrors the Api composition
// root, which needed this to make WebApplicationFactory-based tests see their config overrides.
string ResolveJobsRootPath() =>
    string.IsNullOrWhiteSpace(builder.Configuration["Jobs:RootPath"])
        ? Path.Combine(AppContext.BaseDirectory, "jobs-data")
        : builder.Configuration["Jobs:RootPath"]!;

builder.Services.AddSingleton(_ => new UploadLimits(
    builder.Configuration.GetValue("Jobs:MaxAudioBytes", 2_147_483_648L),
    builder.Configuration.GetValue("Jobs:MaxImageBytes", 26_214_400L),
    builder.Configuration.GetValue("Jobs:MaxDurationSeconds", 21_600),
    builder.Configuration.GetValue("Jobs:MinFreeDiskBytes", 3_221_225_472L)));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IFileSignatureValidator, FileSignatureValidator>();
builder.Services.AddSingleton(sp => new FileSystemJobStore(ResolveJobsRootPath(), sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton<IJobRepository>(sp => sp.GetRequiredService<FileSystemJobStore>());
builder.Services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<FileSystemJobStore>());
builder.Services.AddSingleton<IJobFileStorage>(sp => new FileSystemJobFileStorage(
    ResolveJobsRootPath(),
    sp.GetRequiredService<IFileSignatureValidator>(),
    sp.GetRequiredService<UploadLimits>()));
builder.Services.AddSingleton<IJobInputFileLocator>(_ => new FileSystemJobInputFileLocator(ResolveJobsRootPath()));
builder.Services.AddSingleton<IAudioProbe, FfmpegAudioProbe>();
builder.Services.AddSingleton<IVideoRenderer>(sp =>
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

// Singleton, not scoped: JobPollingService is itself a singleton hosted service and consumes this
// directly (no per-request scope exists in a Worker), and all of its own dependencies above are
// singletons too, so this is safe - it holds no per-job mutable state of its own.
builder.Services.AddSingleton<IProcessRenderJobUseCase, ProcessRenderJobUseCase>();

builder.Services.AddHostedService<JobPollingService>();
builder.Services.AddHostedService<CleanupService>();

var host = builder.Build();
host.Run();
