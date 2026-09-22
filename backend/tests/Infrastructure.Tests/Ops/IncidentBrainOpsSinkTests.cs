using System.Net;
using System.Text;
using DjVisualizer.Application.Abstractions;
using DjVisualizer.Infrastructure.Ops;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Ops;

public class IncidentBrainOpsSinkTests
{
    [Fact]
    public async Task PublishAsync_Posts_The_Keyed_Batch_To_Ingest()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.Accepted };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://incident.example/") };
        var sink = new IncidentBrainOpsSink(http, new IncidentBrainOpsOptions
        {
            IncidentBrainUrl = "https://incident.example",
            IngestKey = "shared-secret",
            Service = "dj-worker",
        });

        await sink.PublishAsync(
            new OpsEvent("dj-worker", "error", "job abc failed: Rendering failed.", new DateTime(2026, 9, 22, 4, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        handler.Last.Should().NotBeNull();
        handler.Last!.Method.Should().Be(HttpMethod.Post);
        handler.Last.RequestUri!.ToString().Should().Be("https://incident.example/api/ingest/logs");
        handler.Last.Headers.GetValues(IncidentBrainOpsSink.KeyHeader).Single().Should().Be("shared-secret");
        handler.Body.Should().Contain("dj-worker");
        handler.Body.Should().Contain("job abc failed");
        handler.Body.Should().Contain("error");
    }

    [Fact]
    public async Task PublishAsync_Does_Not_Throw_When_The_Ops_Box_Returns_An_Error()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.ServiceUnavailable };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://incident.example/") };
        var sink = new IncidentBrainOpsSink(http, new IncidentBrainOpsOptions
        {
            IncidentBrainUrl = "https://incident.example",
            IngestKey = "shared-secret",
        });

        var act = () => sink.PublishAsync(
            new OpsEvent("dj-worker", "error", "job abc failed", DateTime.UtcNow),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_Does_Not_Throw_When_The_Handler_Throws()
    {
        var handler = new ThrowingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://incident.example/") };
        var sink = new IncidentBrainOpsSink(http, new IncidentBrainOpsOptions
        {
            IncidentBrainUrl = "https://incident.example",
            IngestKey = "shared-secret",
        });

        var act = () => sink.PublishAsync(
            new OpsEvent("dj-worker", "error", "job abc failed", DateTime.UtcNow),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;
        public HttpRequestMessage? Last { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent("{\"accepted\":1}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
