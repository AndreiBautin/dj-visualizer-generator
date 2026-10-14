using DjVisualizer.Worker.Hosting;
using FluentAssertions;

namespace DjVisualizer.Worker.Tests;

public class InstanceLeaseServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "djvisualizer-lease-" + Guid.NewGuid().ToString("N"));

    public InstanceLeaseServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Exactly one worker may own a jobs directory. A second must fail its start with a
    /// message that says why, and a graceful stop must release the lease for a replacement.</summary>
    [Fact]
    public async Task A_Second_Instance_Fails_To_Start_Until_The_First_Stops()
    {
        using var first = new InstanceLeaseService(_root, "worker");
        using var second = new InstanceLeaseService(_root, "worker");
        await first.StartAsync(CancellationToken.None);

        var act = () => second.StartAsync(CancellationToken.None);
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Only one worker*");

        await first.StopAsync(CancellationToken.None);
        await second.Invoking(s => s.StartAsync(CancellationToken.None)).Should().NotThrowAsync();
    }
}
