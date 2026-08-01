using System.Threading.RateLimiting;
using DjVisualizer.Api.Configuration;
using DjVisualizer.Api.HealthChecks;
using DjVisualizer.Api.Middleware;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Audio;
using DjVisualizer.Infrastructure.Jobs;
using DjVisualizer.Infrastructure.Time;
using DjVisualizer.Infrastructure.Uploads;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DiskSpaceHealthCheck>("disk-space");
builder.Services.AddProblemDetails();

builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));

// All configuration reads below are deferred into factory delegates (rather than read eagerly
// into local variables here) so they run after the host is fully built, when every configuration
// source - including the ones a test's WebApplicationFactory layers on - has been merged in.
string ResolveJobsRootPath(JobsOptions options) =>
    string.IsNullOrWhiteSpace(options.RootPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "jobs-data")
        : options.RootPath;

builder.Services.Configure<FormOptions>(options =>
{
    var jobsOptions = builder.Configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();
    options.MultipartBodyLengthLimit = jobsOptions.MaxAudioBytes + jobsOptions.MaxImageBytes + 1_048_576;
});
builder.WebHost.ConfigureKestrel(options =>
{
    var jobsOptions = builder.Configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();
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
builder.Services.AddScoped<ICreateJobUseCase, CreateJobUseCase>();
builder.Services.AddScoped<IGetJobStatusUseCase, GetJobStatusUseCase>();
builder.Services.AddScoped<IGetJobDownloadUseCase, GetJobDownloadUseCase>();

var app = builder.Build();

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

app.Run();

public partial class Program;
