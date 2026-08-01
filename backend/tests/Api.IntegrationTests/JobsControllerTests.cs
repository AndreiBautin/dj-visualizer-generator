using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DjVisualizer.Api.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DjVisualizer.Api.IntegrationTests;

public class JobsControllerTests : IDisposable
{
    private static readonly byte[] ValidMp3Bytes =
        [0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04];

    private static readonly byte[] ValidPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

    private readonly JobsApiFactory _factory = new();
    private readonly HttpClient _client;

    public JobsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static MultipartFormDataContent BuildValidForm(
        string title = "Friday Night Set",
        string preset = "1080p",
        string audioFileName = "mix.mp3",
        byte[]? audioBytes = null,
        string artworkFileName = "cover.png",
        double? rotationSpeedSeconds = null,
        string? captionFont = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(title), "Title" },
            { new StringContent(preset), "Preset" },
        };

        if (rotationSpeedSeconds.HasValue)
        {
            content.Add(new StringContent(rotationSpeedSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "RotationSpeedSeconds");
        }

        if (captionFont is not null)
        {
            content.Add(new StringContent(captionFont), "CaptionFont");
        }

        var audioContent = new ByteArrayContent(audioBytes ?? ValidMp3Bytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        content.Add(audioContent, "Audio", audioFileName);

        var artworkContent = new ByteArrayContent(ValidPngBytes);
        artworkContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(artworkContent, "Artwork", artworkFileName);

        return content;
    }

    [Fact]
    public async Task Post_Jobs_Returns_Created_With_A_Job_Id_On_The_Happy_Path()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("jobId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_Jobs_Accepts_A_Custom_RotationSpeed_And_CaptionFont()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm(rotationSpeedSeconds: 5.0, captionFont: "mono-bold"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_Jobs_Rejects_A_RotationSpeed_Outside_The_Allowed_Range()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm(rotationSpeedSeconds: 100.0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Jobs_Rejects_An_Unknown_CaptionFont()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm(captionFont: "comic-sans"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Jobs_By_Id_Returns_Queued_Status_Right_After_Creation()
    {
        var createResponse = await _client.PostAsync("/jobs", BuildValidForm());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString();

        var statusResponse = await _client.GetAsync($"/jobs/{jobId}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        status.GetProperty("status").GetString().Should().Be("Queued");
        status.GetProperty("progress").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Get_Jobs_By_Id_Returns_NotFound_For_An_Unknown_Id()
    {
        var response = await _client.GetAsync($"/jobs/{Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Jobs_By_Id_Returns_NotFound_For_A_Malformed_Id()
    {
        var response = await _client.GetAsync("/jobs/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Jobs_Rejects_A_Missing_Title_With_A_Problem_Details_Response()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm(title: "   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_Jobs_Rejects_An_Executable_Disguised_As_Audio()
    {
        byte[] exeHeader = [0x4D, 0x5A, 0x90, 0x00];

        var response = await _client.PostAsync("/jobs", BuildValidForm(audioBytes: exeHeader));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Jobs_Rejects_Audio_Longer_Than_The_Configured_Maximum_Duration()
    {
        _factory.StubAudioDuration = TimeSpan.FromHours(7);

        var response = await _client.PostAsync("/jobs", BuildValidForm());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Jobs_Ignores_A_Path_Traversal_Attempt_In_The_Uploaded_File_Name()
    {
        var response = await _client.PostAsync("/jobs", BuildValidForm(audioFileName: "../../../etc/passwd.mp3"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString();
        var expectedPath = Path.Combine(_factory.JobsRootPath, jobId!, "input", "audio.mp3");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Responses_Include_Baseline_Security_Headers()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
    }

    [Fact]
    public async Task Post_Jobs_Rate_Limits_Repeated_Requests_From_The_Same_Client()
    {
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await _client.PostAsync("/jobs", BuildValidForm());
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Get_Download_Returns_NotFound_For_An_Unknown_Job()
    {
        var response = await _client.GetAsync($"/jobs/{Guid.NewGuid():D}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Download_Returns_Conflict_When_The_Job_Has_Not_Finished_Rendering()
    {
        var createResponse = await _client.PostAsync("/jobs", BuildValidForm());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString();

        var response = await _client.GetAsync($"/jobs/{jobId}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_Download_Streams_The_Video_For_A_Completed_Job()
    {
        var createResponse = await _client.PostAsync("/jobs", BuildValidForm(title: "Friday Night: Deep House"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString()!;
        var videoBytes = await CompleteJobWithFakeVideoAsync(jobId);

        var response = await _client.GetAsync($"/jobs/{jobId}/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        response.Content.Headers.ContentDisposition!.ToString().Should().Contain("Friday Night_ Deep House.mp4");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(videoBytes);
    }

    private async Task<byte[]> CompleteJobWithFakeVideoAsync(string jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<DjVisualizer.Application.Abstractions.IJobRepository>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<DjVisualizer.Application.Abstractions.IJobFileStorage>();
        var id = DjVisualizer.Domain.Jobs.JobId.Parse(jobId);

        var job = await jobRepository.FindAsync(id, CancellationToken.None);
        job!.Start(DateTimeOffset.UtcNow);
        job.Complete(DateTimeOffset.UtcNow);
        await jobRepository.SaveAsync(job, CancellationToken.None);

        var outputPath = await fileStorage.PrepareOutputFilePathAsync(id, CancellationToken.None);
        byte[] videoBytes = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(outputPath, videoBytes);
        return videoBytes;
    }
}
