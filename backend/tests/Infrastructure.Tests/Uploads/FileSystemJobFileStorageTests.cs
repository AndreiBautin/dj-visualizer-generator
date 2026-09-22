using DjVisualizer.Domain.Jobs;
using DjVisualizer.Domain.Uploads;
using DjVisualizer.Infrastructure.Uploads;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Uploads;

public class FileSystemJobFileStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "djvisualizer-tests", Guid.NewGuid().ToString("N"));

    private static readonly byte[] ValidMp3Bytes =
        [0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

    private static readonly byte[] ValidPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

    public FileSystemJobFileStorageTests()
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

    private FileSystemJobFileStorage CreateSut(UploadLimits? limits = null) =>
        new(_rootPath, new FileSignatureValidator(), limits ?? new UploadLimits(1_000_000, 1_000_000, 21_600, 1_000_000));

    [Fact]
    public async Task SaveAudioAsync_Writes_A_Valid_File_To_The_Jobs_Input_Directory()
    {
        var sut = CreateSut();
        var jobId = JobId.New();

        var result = await sut.SaveAudioAsync(jobId, new MemoryStream(ValidMp3Bytes), "mix.mp3", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AbsolutePath.Should().Be(Path.Combine(_rootPath, jobId.ToString(), "input", "audio.mp3"));
        result.Value.SizeBytes.Should().Be(ValidMp3Bytes.Length);
        File.Exists(result.Value.AbsolutePath).Should().BeTrue();
        (await File.ReadAllBytesAsync(result.Value.AbsolutePath)).Should().BeEquivalentTo(ValidMp3Bytes);
    }

    [Fact]
    public async Task SaveArtworkAsync_Writes_A_Valid_File_To_The_Jobs_Input_Directory()
    {
        var sut = CreateSut();
        var jobId = JobId.New();

        var result = await sut.SaveArtworkAsync(jobId, new MemoryStream(ValidPngBytes), "cover.png", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AbsolutePath.Should().Be(Path.Combine(_rootPath, jobId.ToString(), "input", "artwork.png"));
        File.Exists(result.Value.AbsolutePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAudioAsync_Rejects_An_Unsupported_Extension_Without_Writing_A_File()
    {
        var sut = CreateSut();
        var jobId = JobId.New();

        var result = await sut.SaveAudioAsync(jobId, new MemoryStream(ValidMp3Bytes), "payload.exe", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        Directory.Exists(Path.Combine(_rootPath, jobId.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAudioAsync_Rejects_A_File_Whose_Signature_Does_Not_Match_Its_Extension()
    {
        var sut = CreateSut();
        var jobId = JobId.New();

        var result = await sut.SaveAudioAsync(jobId, new MemoryStream(ValidPngBytes), "mix.mp3", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        Directory.Exists(Path.Combine(_rootPath, jobId.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAudioAsync_Rejects_And_Cleans_Up_A_File_Exceeding_The_Size_Limit()
    {
        var sut = CreateSut(new UploadLimits(maxAudioBytes: 10, maxImageBytes: 1_000_000, maxDurationSeconds: 21_600, minFreeDiskBytes: 1_000_000));
        var jobId = JobId.New();

        var result = await sut.SaveAudioAsync(jobId, new MemoryStream(ValidMp3Bytes), "mix.mp3", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        File.Exists(Path.Combine(_rootPath, jobId.ToString(), "input", "audio.mp3")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteJobFilesAsync_Removes_The_Jobs_Entire_Input_Directory()
    {
        var sut = CreateSut();
        var jobId = JobId.New();
        await sut.SaveAudioAsync(jobId, new MemoryStream(ValidMp3Bytes), "mix.mp3", CancellationToken.None);

        await sut.DeleteJobFilesAsync(jobId, CancellationToken.None);

        Directory.Exists(Path.Combine(_rootPath, jobId.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task PrepareOutputFilePathAsync_Creates_The_Output_Directory_And_Returns_The_Video_Path()
    {
        var sut = CreateSut();
        var jobId = JobId.New();

        var path = await sut.PrepareOutputFilePathAsync(jobId, CancellationToken.None);

        path.Should().Be(Path.Combine(_rootPath, jobId.ToString(), "output", "video.mp4"));
        Directory.Exists(Path.Combine(_rootPath, jobId.ToString(), "output")).Should().BeTrue();
    }

    [Fact]
    public async Task GetRenderedVideoAsync_Returns_Null_When_No_Video_Has_Been_Rendered()
    {
        var sut = CreateSut();

        var video = await sut.GetRenderedVideoAsync(JobId.New(), CancellationToken.None);

        video.Should().BeNull();
    }

    /// <summary>
    /// The size matters as much as the path: it is what gets charged against the egress budget,
    /// so a wrong or zero value would let the instance serve past its cap without noticing.
    /// </summary>
    [Fact]
    public async Task GetRenderedVideoAsync_Returns_The_Path_And_Size_When_A_Video_Exists()
    {
        var sut = CreateSut();
        var jobId = JobId.New();
        var preparedPath = await sut.PrepareOutputFilePathAsync(jobId, CancellationToken.None);
        await File.WriteAllBytesAsync(preparedPath, [1, 2, 3]);

        var video = await sut.GetRenderedVideoAsync(jobId, CancellationToken.None);

        video.Should().NotBeNull();
        video!.FilePath.Should().Be(preparedPath);
        video.SizeBytes.Should().Be(3);
    }
}
