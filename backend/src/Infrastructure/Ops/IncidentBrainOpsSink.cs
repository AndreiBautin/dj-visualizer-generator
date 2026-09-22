using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Infrastructure.Ops;

/// <summary>
/// Posts a single log batch to Incident Intelligence <c>POST /api/ingest/logs</c>. Failures are
/// swallowed: a down ops box must not fail a render job that already failed for its own reason.
/// No logger on purpose. Infrastructure stays package-free; the use case already logs if this throws.
/// </summary>
public sealed class IncidentBrainOpsSink(HttpClient http, IncidentBrainOpsOptions options) : IOpsEventSink
{
    public const string KeyHeader = "X-Ingest-Key";
    public const string IngestPath = "api/ingest/logs";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task PublishAsync(OpsEvent evt, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, IngestPath);
            request.Headers.TryAddWithoutValidation(KeyHeader, options.IngestKey);
            var payload = new
            {
                logs = new[]
                {
                    new
                    {
                        timestamp = evt.Timestamp,
                        service = evt.Service,
                        level = evt.Level,
                        message = evt.Message,
                    },
                },
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await http.SendAsync(request, cancellationToken);
            _ = response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallow. The job is already Failed in the store.
        }
    }
}
