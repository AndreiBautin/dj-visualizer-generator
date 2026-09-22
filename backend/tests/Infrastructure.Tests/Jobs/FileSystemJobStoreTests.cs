using DjVisualizer.Application.Abstractions;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Jobs;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Jobs;

public class FileSystemJobStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "djvisualizer-tests", Guid.NewGuid().ToString("N"));

    public FileSystemJobStoreTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public void Instance_Lease_Rejects_A_Second_Owner_And_Releases_On_Dispose()
    {
        using (var first = new FileSystemInstanceLease(_rootPath, "worker"))
        {
            var second = () => new FileSystemInstanceLease(_rootPath, "worker");
            second.Should().Throw<IOException>();
        }
        using var replacement = new FileSystemInstanceLease(_rootPath, "worker");
    }

    private FileSystemJobStore CreateSut() => new(_rootPath, new FixedClock(Now));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private static Job CreateJob() => Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);

    [Fact]
    public async Task FindAsync_Returns_Null_When_No_Job_Exists()
    {
        var sut = CreateSut();

        var found = await sut.FindAsync(JobId.New(), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task EnqueueAsync_Then_FindAsync_Roundtrips_A_Newly_Created_Job()
    {
        var sut = CreateSut();
        var job = CreateJob();

        await sut.EnqueueAsync(job, CancellationToken.None);
        var found = await sut.FindAsync(job.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(job.Id);
        found.Title.Value.Should().Be("Friday Night Set");
        found.Preset.Should().Be(VideoPreset.FullHd1080p);
        found.Status.Should().Be(JobStatus.Queued);
        found.Progress.Should().Be(0);
        found.ErrorMessage.Should().BeNull();
        found.CreatedAt.Should().Be(Now);
        found.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task EnqueueAsync_Then_FindAsync_Roundtrips_A_NonDefault_RotationSpeed_And_CaptionFont()
    {
        var sut = CreateSut();
        var job = Job.Create(
            JobTitle.Create("Friday Night Set"),
            VideoPreset.FullHd1080p,
            RotationSpeed.Create(5.5),
            CaptionFont.SerifBold,
            Now);

        await sut.EnqueueAsync(job, CancellationToken.None);
        var found = await sut.FindAsync(job.Id, CancellationToken.None);

        found!.RotationSpeed.SecondsPerRotation.Should().Be(5.5);
        found.CaptionFont.Should().Be(CaptionFont.SerifBold);
    }

    [Fact]
    public async Task FindAsync_Falls_Back_To_Default_RotationSpeed_And_CaptionFont_For_A_Legacy_Job_File()
    {
        var sut = CreateSut();
        var id = JobId.New();
        var jobDirectory = Path.Combine(_rootPath, id.ToString());
        Directory.CreateDirectory(jobDirectory);
        await File.WriteAllTextAsync(Path.Combine(jobDirectory, "status.json"), $$"""
            {
              "Id": "{{id}}",
              "Title": "Legacy Set",
              "Preset": "1080p",
              "Status": "Queued",
              "Progress": 0,
              "ErrorMessage": null,
              "CreatedAt": "2026-07-31T12:00:00+00:00",
              "UpdatedAt": "2026-07-31T12:00:00+00:00"
            }
            """);

        var found = await sut.FindAsync(id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.RotationSpeed.Should().Be(RotationSpeed.Default);
        found.CaptionFont.Should().Be(CaptionFont.Default);
    }

    [Fact]
    public async Task SaveAsync_Overwrites_The_Previously_Persisted_State()
    {
        var sut = CreateSut();
        var job = CreateJob();
        await sut.EnqueueAsync(job, CancellationToken.None);

        job.Start(Now.AddSeconds(1));
        job.UpdateProgress(77, Now.AddSeconds(2));
        await sut.SaveAsync(job, CancellationToken.None);

        var found = await sut.FindAsync(job.Id, CancellationToken.None);

        found!.Status.Should().Be(JobStatus.Processing);
        found.Progress.Should().Be(77);
        found.UpdatedAt.Should().Be(Now.AddSeconds(2));
    }

    [Fact]
    public async Task SaveAsync_Persists_A_Failed_Job_With_Its_Error_Message()
    {
        var sut = CreateSut();
        var job = CreateJob();
        job.Fail("ffmpeg exited with code 1", Now);

        await sut.SaveAsync(job, CancellationToken.None);
        var found = await sut.FindAsync(job.Id, CancellationToken.None);

        found!.Status.Should().Be(JobStatus.Failed);
        found.ErrorMessage.Should().Be("ffmpeg exited with code 1");
    }

    [Fact]
    public async Task TryDequeueNextAsync_Returns_Null_When_Nothing_Is_Queued()
    {
        var sut = CreateSut();

        var dequeued = await sut.TryDequeueNextAsync(CancellationToken.None);

        dequeued.Should().BeNull();
    }

    [Fact]
    public async Task TryDequeueNextAsync_Claims_The_Oldest_Queued_Job_And_Marks_It_Processing()
    {
        var sut = CreateSut();
        var older = Job.Create(JobTitle.Create("Older Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        var newer = Job.Create(JobTitle.Create("Newer Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now.AddMinutes(5));
        await sut.EnqueueAsync(newer, CancellationToken.None);
        await sut.EnqueueAsync(older, CancellationToken.None);

        var dequeued = await sut.TryDequeueNextAsync(CancellationToken.None);

        dequeued.Should().NotBeNull();
        dequeued!.Id.Should().Be(older.Id);
        dequeued.Status.Should().Be(JobStatus.Processing);

        var persisted = await sut.FindAsync(older.Id, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Processing);
    }

    [Fact]
    public async Task TryDequeueNextAsync_Never_Returns_The_Same_Job_Twice()
    {
        var sut = CreateSut();
        var job = CreateJob();
        await sut.EnqueueAsync(job, CancellationToken.None);

        var first = await sut.TryDequeueNextAsync(CancellationToken.None);
        var second = await sut.TryDequeueNextAsync(CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public async Task TryDequeueNextAsync_Ignores_Jobs_That_Are_Already_Processing_Or_Terminal()
    {
        var sut = CreateSut();
        var processing = Job.Create(JobTitle.Create("Already Processing"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        processing.Start(Now);
        var completed = Job.Create(JobTitle.Create("Already Completed"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        completed.Start(Now);
        completed.Complete(Now);
        await sut.SaveAsync(processing, CancellationToken.None);
        await sut.SaveAsync(completed, CancellationToken.None);

        var dequeued = await sut.TryDequeueNextAsync(CancellationToken.None);

        dequeued.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Returns_Every_Persisted_Job()
    {
        var sut = CreateSut();
        var first = Job.Create(JobTitle.Create("First"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        var second = Job.Create(JobTitle.Create("Second"), VideoPreset.Hd720p, RotationSpeed.Default, CaptionFont.Default, Now);
        await sut.EnqueueAsync(first, CancellationToken.None);
        await sut.EnqueueAsync(second, CancellationToken.None);

        var all = new List<Job>();
        await foreach (var job in sut.GetAllAsync(CancellationToken.None))
        {
            all.Add(job);
        }

        all.Select(j => j.Id.ToString()).Should().BeEquivalentTo([first.Id.ToString(), second.Id.ToString()]);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Nothing_For_An_Empty_Store()
    {
        var sut = CreateSut();

        var all = new List<Job>();
        await foreach (var job in sut.GetAllAsync(CancellationToken.None))
        {
            all.Add(job);
        }

        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Removes_The_Jobs_Folder_Entirely()
    {
        var sut = CreateSut();
        var job = CreateJob();
        await sut.EnqueueAsync(job, CancellationToken.None);

        await sut.DeleteAsync(job.Id, CancellationToken.None);

        (await sut.FindAsync(job.Id, CancellationToken.None)).Should().BeNull();
        Directory.Exists(Path.Combine(_rootPath, job.Id.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_Is_A_NoOp_For_An_Unknown_Job()
    {
        var sut = CreateSut();

        var act = () => sut.DeleteAsync(JobId.New(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_Then_FindAsync_Roundtrips_The_Download_Count()
    {
        var sut = CreateSut();
        var job = CreateJob();
        job.Start(Now);
        job.Complete(Now);
        job.RecordDownload();
        job.RecordDownload();

        await sut.SaveAsync(job, CancellationToken.None);
        var found = await sut.FindAsync(job.Id, CancellationToken.None);

        found!.DownloadCount.Should().Be(2);
    }

    /// <summary>
    /// status.json files written before download limiting existed have no DownloadCount property.
    /// Those must rehydrate with a full allowance rather than failing to deserialize - on the
    /// deployed instance the disk is ephemeral, but a self-hosted one carries its jobs across the
    /// upgrade, and a job that would not load is a job whose video is unreachable.
    /// </summary>
    [Fact]
    public async Task FindAsync_Treats_A_Status_File_Without_A_Download_Count_As_Never_Downloaded()
    {
        var sut = CreateSut();
        var jobId = JobId.New();
        var jobDirectory = Path.Combine(_rootPath, jobId.ToString());
        Directory.CreateDirectory(jobDirectory);

        // Hand-authored rather than written and then edited: this is literally the shape the old
        // serializer produced, so the test breaks if that shape stops loading for any reason.
        await File.WriteAllTextAsync(Path.Combine(jobDirectory, "status.json"), $$"""
            {
              "Id": "{{jobId}}",
              "Title": "Friday Night Set",
              "Preset": "1080p",
              "RotationSpeedSecondsPerRotation": 6,
              "CaptionFont": "sans-bold",
              "Status": "Completed",
              "Progress": 100,
              "ErrorMessage": null,
              "CreatedAt": "2026-07-31T12:00:00+00:00",
              "UpdatedAt": "2026-07-31T12:00:00+00:00"
            }
            """);

        var found = await sut.FindAsync(jobId, CancellationToken.None);

        found.Should().NotBeNull();
        found!.DownloadCount.Should().Be(0);
    }
}
