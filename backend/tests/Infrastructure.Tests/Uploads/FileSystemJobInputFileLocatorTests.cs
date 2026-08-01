using DjVisualizer.Domain.Jobs;
using DjVisualizer.Infrastructure.Uploads;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Uploads;

public class FileSystemJobInputFileLocatorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "djvisualizer-tests", Guid.NewGuid().ToString("N"));

    public FileSystemJobInputFileLocatorTests()
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

    private FileSystemJobInputFileLocator CreateSut() => new(_rootPath);

    [Fact]
    public async Task LocateAsync_Finds_Audio_And_Artwork_Regardless_Of_Extension()
    {
        var jobId = JobId.New();
        var inputDir = Path.Combine(_rootPath, jobId.ToString(), "input");
        Directory.CreateDirectory(inputDir);
        File.WriteAllBytes(Path.Combine(inputDir, "audio.flac"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(inputDir, "artwork.jpeg"), [4, 5, 6]);

        var result = await CreateSut().LocateAsync(jobId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.AudioFilePath.Should().Be(Path.Combine(inputDir, "audio.flac"));
        result.ArtworkFilePath.Should().Be(Path.Combine(inputDir, "artwork.jpeg"));
    }

    [Fact]
    public async Task LocateAsync_Returns_Null_When_The_Job_Has_No_Input_Directory()
    {
        var result = await CreateSut().LocateAsync(JobId.New(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LocateAsync_Returns_Null_When_Only_One_File_Is_Present()
    {
        var jobId = JobId.New();
        var inputDir = Path.Combine(_rootPath, jobId.ToString(), "input");
        Directory.CreateDirectory(inputDir);
        File.WriteAllBytes(Path.Combine(inputDir, "audio.mp3"), [1, 2, 3]);

        var result = await CreateSut().LocateAsync(jobId, CancellationToken.None);

        result.Should().BeNull();
    }
}
