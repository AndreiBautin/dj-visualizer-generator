using System.Threading.RateLimiting;
using DjVisualizer.Api.Configuration;
using DjVisualizer.Api.HealthChecks;
using DjVisualizer.Api.Middleware;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Egress;
using DjVisualizer.Infrastructure.Jobs;
using DjVisualizer.Infrastructure.Time;
using DjVisualizer.Infrastructure.Uploads;
using DjVisualizer.Worker;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DiskSpaceHealthCheck>("disk-space");
builder.Services.AddProblemDetails();

// Parsed through JobsOptionsFactory rather than the default binder: on a deployed instance every
// one of these arrives as an environment variable, and the binder aborts host construction on the
// first malformed value. Parsing is deferred into this delegate (see the note below) so it reads
// the fully merged configuration.
builder.Services.AddOptions<JobsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var parsed = JobsOptionsFactory.Create(configuration, out _);
    options.RootPath = parsed.RootPath;
    options.SingleContainer = parsed.SingleContainer;
    options.MaxAudioBytes = parsed.MaxAudioBytes;
    options.MaxImageBytes = parsed.MaxImageBytes;
    options.MaxDurationSeconds = parsed.MaxDurationSeconds;
    options.MinFreeDiskBytes = parsed.MinFreeDiskBytes;
    options.MaxEgressBytesPerWindow = parsed.MaxEgressBytesPerWindow;
    options.EgressWindowHours = parsed.EgressWindowHours;
});
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));

// All configuration reads below are deferred into factory delegates (rather than read eagerly
// into local variables here) so they run after the host is fully built, when every configuration
// source - including the ones a test's WebApplicationFactory layers on - has been merged in.
string ResolveJobsRootPath(JobsOptions options) =>
    string.IsNullOrWhiteSpace(options.RootPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "jobs-data")
        : options.RootPath;

builder.Services.Configure<FormOptions>(options =>
{
    var jobsOptions = JobsOptionsFactory.Create(builder.Configuration, out _);
    options.MultipartBodyLengthLimit = jobsOptions.MaxAudioBytes + jobsOptions.MaxImageBytes + 1_048_576;
});
builder.WebHost.ConfigureKestrel(options =>
{
    var jobsOptions = JobsOptionsFactory.Create(builder.Configuration, out _);
    options.Limits.MaxRequestBodySize = jobsOptions.MaxAudioBytes + jobsOptions.MaxImageBytes + 1_048_576;

    // Multi-GB audio uploads can legitimately take a while to transfer (large file + disk-write
    // contention, e.g. antivirus scanning the temp file on Windows). Kestrel's default minimum
    // data rate (240 bytes/sec after a 5s grace period) aborts the connection with no HTTP
    // response at all if throughput dips below that - indistinguishable client-side from a crash.
    // Multi-hour video downloads have the same shape of risk on the way out.
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("job-creation", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // Downloads are the expensive responses in this app - tens to hundreds of megabytes each,
    // served straight off disk with no CPU to slow an attacker down. Limiting job creation alone
    // left the amplification open: one accepted render could be re-fetched without bound.
    //
    // 20 per 5 minutes is far above a visitor's needs (Job.MaxDownloads caps them at 5 per job
    // anyway) and far below a useful transfer rate for anyone scripting it.
    options.AddPolicy("job-download", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));

    // Status responses are a few hundred bytes, so this is a hammering guard rather than a cost
    // control. Sized against the real client: the SPA polls every 2 seconds for the length of a
    // render, which on the free instance is ~90 seconds, so a visitor watching one job spends
    // ~45 requests a minute. 240 leaves room for several tabs before anyone legitimate is told no.
    options.AddPolicy("job-status", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddSingleton(sp =>
{
    var jobsOptions = sp.GetRequiredService<IOptions<JobsOptions>>().Value;
    return new UploadLimits(
        jobsOptions.MaxAudioBytes,
        jobsOptions.MaxImageBytes,
        jobsOptions.MaxDurationSeconds,
        jobsOptions.MinFreeDiskBytes);
});
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IFileSignatureValidator, FileSignatureValidator>();
builder.Services.AddSingleton<IDiskSpaceChecker>(sp =>
{
    var jobsOptions = sp.GetRequiredService<IOptions<JobsOptions>>().Value;
    return new DriveInfoDiskSpaceChecker(ResolveJobsRootPath(jobsOptions));
});
builder.Services.AddSingleton(sp =>
{
    var jobsOptions = sp.GetRequiredService<IOptions<JobsOptions>>().Value;
    return new FileSystemJobStore(ResolveJobsRootPath(jobsOptions), sp.GetRequiredService<IClock>());
});
builder.Services.AddSingleton<IJobRepository>(sp => sp.GetRequiredService<FileSystemJobStore>());
builder.Services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<FileSystemJobStore>());
builder.Services.AddSingleton<IJobFileStorage>(sp =>
{
    var jobsOptions = sp.GetRequiredService<IOptions<JobsOptions>>().Value;
    return new FileSystemJobFileStorage(
        ResolveJobsRootPath(jobsOptions),
        sp.GetRequiredService<IFileSignatureValidator>(),
        sp.GetRequiredService<UploadLimits>());
});
builder.Services.AddSingleton<IAudioProbe, FfmpegAudioProbe>();

// Singleton because the budget is a property of the instance, not of a request: a per-request
// counter would reset on every download and cap nothing.
builder.Services.AddSingleton<IEgressBudget>(sp =>
{
    var jobsOptions = sp.GetRequiredService<IOptions<JobsOptions>>().Value;
    return new RollingWindowEgressBudget(
        jobsOptions.MaxEgressBytesPerWindow,
        TimeSpan.FromHours(jobsOptions.EgressWindowHours),
        sp.GetRequiredService<IClock>());
});
builder.Services.AddScoped<ICreateJobUseCase, CreateJobUseCase>();
builder.Services.AddScoped<IGetJobStatusUseCase, GetJobStatusUseCase>();
builder.Services.AddSingleton<IHostedService>(sp => new InstanceLeaseService(ResolveJobsRootPath(sp.GetRequiredService<IOptions<JobsOptions>>().Value), "api"));
builder.Services.AddSingleton<JobDownloadGate>();
builder.Services.AddScoped<IGetJobDownloadUseCase, GetJobDownloadUseCase>();

// Single-container hosting: the render worker's background services run in this process rather
// than in a separate container. Identical services either way - see WorkerServiceRegistration.
//
// This is the one value that genuinely cannot be deferred: it decides which services get
// registered, so it must be known before the container is built. Every other read above happens
// inside a delegate instead, so it sees configuration sources layered on after this point - which
// is what lets the integration tests override the jobs root.
var singleContainer = JobsOptionsFactory.Create(builder.Configuration, out _).SingleContainer;
if (singleContainer)
{
    builder.Services.AddRenderWorker(
        builder.Configuration,
        () => ResolveJobsRootPath(JobsOptionsFactory.Create(builder.Configuration, out _)));
}

var app = builder.Build();

// Re-parsed against the built host's configuration, which is the fully merged one, and reported
// now because logging does not exist until this point. Parsing is pure, so doing it twice costs
// nothing. A malformed variable is a warning and a documented fallback, never a failed startup.
JobsOptionsFactory.Create(app.Services.GetRequiredService<IConfiguration>(), out var configurationWarnings);
foreach (var warning in configurationWarnings)
{
    app.Logger.LogWarning("Ignoring malformed configuration value. {Warning}", warning);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (singleContainer)
{
    // Serves the built SPA from wwwroot. In docker-compose this is nginx's job instead, so the
    // static-file middleware is not even registered - the API stays a pure JSON API there.
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Client-side routes (and a refresh on one) must return the SPA document rather than a 404.
    // Registered after MapControllers so it can never shadow a real API route.
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
