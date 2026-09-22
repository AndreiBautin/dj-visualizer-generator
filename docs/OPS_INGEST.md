# Ops ingest (Incident Intelligence)

When a render job fails, the worker posts one log line to Incident Intelligence. The dashboard clusters those the same way it clusters simulator errors, so a burst of bad uploads becomes an incident instead of a line in Render's logs.

This is opt-in. Empty config is a no-op. The public demo stays standalone.

## What gets sent

On `ProcessRenderJobUseCase` fail (missing files, unreadable audio, ffmpeg error):

```
POST {Ops__IncidentBrainUrl}/api/ingest/logs
X-Ingest-Key: {Ops__IngestKey}

{
  "logs": [
    {
      "service": "dj-worker",
      "level": "error",
      "message": "job {id} failed: {public reason}"
    }
  ]
}
```

The public reason is the same string stored on the job. ffmpeg command lines never leave the worker.

## Config

| Variable | Where |
|----------|--------|
| `Ops__IncidentBrainUrl` | DJ worker / single-container API |
| `Ops__IngestKey` | DJ, same value as IIP `Ingest__ApiKey` |
| `Ingest__ApiKey` | Incident Intelligence API |

If either DJ value is missing, `NoOpOpsEventSink` is registered. If the IIP key is empty, ingest returns 503. Neither side requires the other to boot.

A down or slow IIP box cannot fail a job: the sink swallows HTTP errors, and the use case catches anything that still throws after the job is already marked Failed.

## After both are deployed

1. Generate a long random string.
2. Set it on `incident-api` as `Ingest__ApiKey`.
3. Set it on `dj-visualizer` as `Ops__IngestKey`.
4. Set `Ops__IncidentBrainUrl` to the incident-api origin.
5. Force a failed render. Within about ten seconds IIP should open a `dj-worker` incident if the failure repeats enough to trip clustering.
