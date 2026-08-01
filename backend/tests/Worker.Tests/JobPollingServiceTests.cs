using DjVisualizer.Application.Abstractions;
using DjVisualizer.Application.Jobs;
using DjVisualizer.Domain.Jobs;
using DjVisualizer.Worker.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DjVisualizer.Worker.Tests;

public class JobPollingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly IProcessRenderJobUseCase _processRenderJobUseCase = Substitute.For<IProcessRenderJobUseCase>();

    private DjVisualizer.Worker.JobPollingService CreateSut() => new(
        _jobQueue,
        _processRenderJobUseCase,
        Options.Create(new WorkerOptions()),
        NullLogger<DjVisualizer.Worker.JobPollingService>.Instance);

    private static Job CreateProcessingJob()
    {
        var job = Job.Create(JobTitle.Create("Friday Night Set"), VideoPreset.FullHd1080p, RotationSpeed.Default, CaptionFont.Default, Now);
        job.Start(Now);
        return job;
    }

    [Fact]
    public async Task PollOnceAsync_Returns_False_When_Nothing_Is_Queued()
    {
        _jobQueue.TryDequeueNextAsync(Arg.Any<CancellationToken>()).Returns((Job?)null);

        var processed = await CreateSut().PollOnceAsync(CancellationToken.None);

        processed.Should().BeFalse();
        await _processRenderJobUseCase.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task PollOnceAsync_Processes_A_Dequeued_Job_And_Returns_True()
    {
        var job = CreateProcessingJob();
        _jobQueue.TryDequeueNextAsync(Arg.Any<CancellationToken>()).Returns(job);

        var processed = await CreateSut().PollOnceAsync(CancellationToken.None);

        processed.Should().BeTrue();
        await _processRenderJobUseCase.Received(1).ExecuteAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_Returns_False_And_Does_Not_Throw_When_Dequeuing_Fails()
    {
        _jobQueue.TryDequeueNextAsync(Arg.Any<CancellationToken>()).Returns<Job?>(_ => throw new IOException("disk error"));

        var processed = await CreateSut().PollOnceAsync(CancellationToken.None);

        processed.Should().BeFalse();
    }

    [Fact]
    public async Task PollOnceAsync_Swallows_An_Unexpected_Exception_From_Processing_And_Still_Returns_True()
    {
        var job = CreateProcessingJob();
        _jobQueue.TryDequeueNextAsync(Arg.Any<CancellationToken>()).Returns(job);
        _processRenderJobUseCase.ExecuteAsync(job, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("unexpected"));

        var processed = await CreateSut().PollOnceAsync(CancellationToken.None);

        processed.Should().BeTrue();
    }
}
