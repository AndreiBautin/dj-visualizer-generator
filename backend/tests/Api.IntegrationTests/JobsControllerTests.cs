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

    /// <summary>
    /// End to end, because the per-job limit is only worth anything if the count survives the
    /// round trip through status.json - a counter that incremented in memory and was never
    /// persisted would pass a use-case test and reset on every request in production.
    /// </summary>
    [Fact]
    public async Task Get_Download_Stops_Serving_The_Video_Once_The_Per_Job_Limit_Is_Reached()
    {
        var createResponse = await _client.PostAsync("/jobs", BuildValidForm());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString()!;
        await CompleteJobWithFakeVideoAsync(jobId);

        for (var i = 0; i < DjVisualizer.Domain.Jobs.Job.MaxDownloads; i++)
        {
            var allowed = await _client.GetAsync($"/jobs/{jobId}/download");
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"download {i + 1} is within the allowance");
        }

        var refused = await _client.GetAsync($"/jobs/{jobId}/download");

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// The instance-wide cap, which is the one that bounds the hosting bill: the per-job limit and
    /// the per-IP rate limit both scale with the number of jobs and callers, so neither of them
    /// caps a total.
    /// </summary>
    [Fact]
    public async Task Get_Download_Returns_ServiceUnavailable_Once_The_Instance_Egress_Budget_Is_Spent()
    {
        using var factory = new JobsApiFactory
        {
            // Exactly one video's worth (CreateAndCompleteJobAsync writes 10 bytes), so the first
            // download succeeds and spends the window's entire budget.
            ExtraConfiguration = { ["Jobs:MaxEgressBytesPerWindow"] = "10" },
        };
        using var client = factory.CreateClient();

        var jobId = await CreateAndCompleteJobAsync(factory, client);
        var first = await client.GetAsync($"/jobs/{jobId}/download");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // A different job, so this can only be the instance-wide budget refusing - not the
        // per-job limit, which the first job has barely touched.
        var secondJobId = await CreateAndCompleteJobAsync(factory, client);
        var refused = await client.GetAsync($"/jobs/{secondJobId}/download");

        refused.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Zero means unlimited, and it is the default every self-hosted instance runs with. If that
    /// ever inverted, a private deployment would refuse every download on a limit its operator
    /// never set.
    /// </summary>
    [Fact]
    public async Task Get_Download_Is_Not_Capped_When_No_Egress_Budget_Is_Configured()
    {
        var createResponse = await _client.PostAsync("/jobs", BuildValidForm());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString()!;
        await CompleteJobWithFakeVideoAsync(jobId);

        var response = await _client.GetAsync($"/jobs/{jobId}/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Preview_Supports_Seeking_Without_Spending_Downloads()
    {
        var id = await CreateAndCompleteJobAsync(_factory, _client);
        for (var i = 0; i < 6; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/jobs/{id}/preview");
            request.Headers.Range = new RangeHeaderValue(2, 5);
            using var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(3, 4, 5, 6);
            response.Content.Headers.ContentDisposition.Should().BeNull();
        }
        for (var i = 0; i < 5; i++)
        {
            using var response = await _client.GetAsync($"/jobs/{id}/download");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Concurrent_Downloads_Admit_Exactly_Five_Requests()
    {
        var id = await CreateAndCompleteJobAsync(_factory, _client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync($"/jobs/{id}/download")));
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(5);
        responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(5);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Preview_Still_Respects_Instance_Egress_Budget()
    {
        using var factory = new JobsApiFactory { ExtraConfiguration = { ["Jobs:MaxEgressBytesPerWindow"] = "10" } };
        using var client = factory.CreateClient();
        var id = await CreateAndCompleteJobAsync(factory, client);
        (await client.GetAsync($"/jobs/{id}/preview")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/jobs/{id}/preview")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<string> CreateAndCompleteJobAsync(JobsApiFactory factory, HttpClient client)
    {
        var createResponse = await client.PostAsync("/jobs", BuildValidForm());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetString()!;

        using var scope = factory.Services.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<DjVisualizer.Application.Abstractions.IJobRepository>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<DjVisualizer.Application.Abstractions.IJobFileStorage>();
        var id = DjVisualizer.Domain.Jobs.JobId.Parse(jobId);

        var job = await jobRepository.FindAsync(id, CancellationToken.None);
        job!.Start(DateTimeOffset.UtcNow);
        job.Complete(DateTimeOffset.UtcNow);
        await jobRepository.SaveAsync(job, CancellationToken.None);

        var outputPath = await fileStorage.PrepareOutputFilePathAsync(id, CancellationToken.None);
        await File.WriteAllBytesAsync(outputPath, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        return jobId;
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
