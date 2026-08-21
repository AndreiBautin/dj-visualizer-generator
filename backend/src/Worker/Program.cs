using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Jobs;
using DjVisualizer.Infrastructure.Time;
using DjVisualizer.Infrastructure.Uploads;
using DjVisualizer.Worker;

var builder = Host.CreateApplicationBuilder(args);


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
builder.Services.AddSingleton<IAudioProbe, FfmpegAudioProbe>();

// Everything specific to rendering lives in AddRenderWorker, so the API can host exactly the
// same services in-process when it runs as a single container. See WorkerServiceRegistration.
builder.Services.AddRenderWorker(builder.Configuration, ResolveJobsRootPath);

var host = builder.Build();
host.Run();
