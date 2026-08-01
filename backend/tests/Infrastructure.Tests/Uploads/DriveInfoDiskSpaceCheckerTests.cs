using DjVisualizer.Infrastructure.Uploads;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Uploads;

public class DriveInfoDiskSpaceCheckerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "djvisualizer-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public void GetAvailableFreeBytes_Returns_A_Positive_Value_For_A_Real_Path()
    {
        var sut = new DriveInfoDiskSpaceChecker(_rootPath);

        var freeBytes = sut.GetAvailableFreeBytes();

        freeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetAvailableFreeBytes_Creates_The_Root_Directory_If_Missing()
    {
        var sut = new DriveInfoDiskSpaceChecker(_rootPath);

        sut.GetAvailableFreeBytes();

        Directory.Exists(_rootPath).Should().BeTrue();
    }
}
