using System.Net.Http.Json;
using DjVisualizer.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DjVisualizer.Infrastructure.Ops;

/// <summary>
/// Posts a single log batch to Incident Intelligence <c>POST /api/ingest/logs</c>. Failures are
/// swallowed: a down ops box must not fail a render job that already failed for its own reason.
/// </summary>
public sealed class IncidentBrainOpsSink(
    HttpClient http,
    IncidentBrainOpsOptions options,
    ILogger<IncidentBrainOpsSink> logger) : IOpsEventSink
{
    public const string KeyHeader = "X-Ingest-Key";
    public const string IngestPath = "api/ingest/logs";

    public async Task PublishAsync(OpsEvent evt, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, IngestPath);
            request.Headers.TryAddWithoutValidation(KeyHeader, options.IngestKey);
            request.Content = JsonContent.Create(new
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
            });

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Incident Intelligence ingest returned {Status} for {Service}.",
                    (int)response.StatusCode,
                    evt.Service);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Incident Intelligence ingest failed; the render path is unaffected.");
        }
    }
}
