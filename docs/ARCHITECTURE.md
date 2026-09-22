# Architecture

## The shape, and why

```
                     ┌───────────────────────────────────────────┐
                     │  React SPA (Vite, TanStack Query, RHF+Zod) │
                     └──────────────────┬────────────────────────┘
                                        │ HTTP (multipart upload, JSON polling)
                     ┌──────────────────▼────────────────────────┐
 composition root →  │  Api  (ASP.NET Core 9, controllers)       │
                     └──────────────────┬────────────────────────┘
                                        │
                     ┌──────────────────▼────────────────────────┐
                     │  Application — use cases + the interfaces │
                     │  CreateJob / GetJobStatus / GetJobDownload│
                     │  ProcessRenderJob                         │
                     └──────────────────┬────────────────────────┘
                                        │ depends only on ↓
                     ┌──────────────────▼────────────────────────┐
                     │  Domain — Job, JobId, JobTitle,           │
                     │  VideoPreset, RotationSpeed, UploadLimits │
                     │  (zero package references)                │
                     └──────────────────▲────────────────────────┘
                                        │ implements those interfaces
                     ┌──────────────────┴────────────────────────┐
                     │  Infrastructure — FileSystemJobStore,     │
                     │  FfmpegVideoRenderer, FfmpegAudioProbe,   │
                     │  FileSignatureValidator                   │
                     └──────────────────┬────────────────────────┘
                                        │ reads / writes
                     ┌──────────────────▼────────────────────────┐
                     │  jobs-data/<job-id>/                      │
                     │    status.json   ← queue AND repository   │
                     │    input/audio.*   input/artwork.*        │
                     │    output/video.mp4                       │
                     └──────────────────▲────────────────────────┘
                                        │ polls for Queued jobs
 composition root →  ┌──────────────────┴────────────────────────┐
                     │  Worker.Hosting — JobPollingService,      │
                     │  CleanupService                           │
                     └───────────────────────────────────────────┘
```

Dependencies point inward. `Domain` references nothing but the BCL. `Application` references only
`Domain` and declares the interfaces it needs (`IJobRepository`, `IJobQueue`, `IVideoRenderer`,
`IAudioProbe`, `IClock`, …). `Infrastructure` implements them. `Api` and `Worker` are composition
roots: they wire dependencies and hold no business logic.

## The three deliberate absences

Understanding this codebase is mostly understanding what is deliberately *not* in it.

**No database.** A job's state is a `status.json` file in its own directory.
`FileSystemJobStore` implements *both* `IJobRepository` and `IJobQueue` over that one directory
tree. The domain is a single short-lived entity with a four-state lifecycle, no relationships and
no queries — a database would add something to operate, migrate and back up in exchange for
nothing.

**No message broker.** `JobPollingService` polls the jobs directory.
`FileSystemJobStore.TryDequeueNextAsync` claims the oldest `Queued` job by transitioning it to
`Processing` and persisting *before* returning it, so the claim and the write are the same act.
This assumes a single worker instance — stated openly rather than hidden.

**No auth.** Anyone may create a job; a job is reachable by its GUID and nothing else. There are
no accounts, so there is nothing to own and no authorization question to answer. What that costs
is set out in [SECURITY.md](SECURITY.md).

## Two composition roots, one set of services

The application deploys in two shapes, and this is the one structural thing worth knowing:

| | `docker-compose.yml` | `docker/Dockerfile.singlecontainer` |
|---|---|---|
| Processes | nginx + Api + Worker | one: Api |
| Renders in | the Worker container | the Api process |
| SPA served by | nginx | the Api, from `wwwroot` |
| Selected by | default | `Jobs__SingleContainer=true` |

Single-container mode is **not a fork**. `Worker.Hosting` is a library holding
`JobPollingService`, `CleanupService` and `WorkerServiceRegistration.AddRenderWorker`. The Worker
executable calls `AddRenderWorker`; so does `Api/Program.cs` when configured to. The hosted
services, the use case and every dependency are the same types either way — only the host differs.
That is what makes free single-instance hosting possible without maintaining a second application.

`Worker.Hosting` is a class library rather than part of the Worker executable for a concrete
reason: referencing the executable made both entry points' `appsettings.json` collide on publish.

## One request, traced end to end

`POST /jobs` with a multipart body — the interesting path, because it crosses every layer.

1. **[`JobsController.Create`](../backend/src/Api/Controllers/JobsController.cs)**
   Model-bound to `Contracts/CreateJobFormRequest`. Rate-limited by the `job-creation` policy
   (10/minute per IP, configured in `Api/Program.cs`). Opens both `IFormFile` streams and passes
   them to the use case. It never touches the filesystem itself.

2. **[`CreateJobUseCase.ExecuteAsync`](../backend/src/Application/Jobs/CreateJobUseCase.cs)**
   Checks free disk via `IDiskSpaceChecker` (low → `Unavailable`, surfaced as 503), then builds
   the domain values: `JobTitle.Create`, `VideoPreset.FromName`, `RotationSpeed.Create`,
   `CaptionFont.FromName`. Each throws its own domain exception, which the use case converts into
   a validation `Error`. **Input validation lives in the Domain constructors, not the controller.**

3. **[`Job.Create`](../backend/src/Domain/Jobs/Job.cs)**
   Produces a `Queued` job with a fresh `JobId`. Transitions are guarded by
   `EnsureTransitionAllowed`, so an illegal move — completing a queued job, failing a completed
   one — throws instead of silently corrupting state.

4. **[`FileSystemJobFileStorage.SaveAudioAsync`](../backend/src/Infrastructure/Uploads/FileSystemJobFileStorage.cs)**
   Extension allowlist, then 16 header bytes checked against `FileSignatureValidator` — an `.mp3`
   whose bytes are a Windows executable is rejected. Then streams to disk, counting bytes and
   aborting past `UploadLimits.MaxAudioBytes`. The size limit is enforced **while writing**, never
   by trusting `Content-Length`.

5. **[`FfmpegAudioProbe`](../backend/src/Infrastructure/Audio/FfmpegAudioProbe.cs)**
   Shells out to `ffprobe` for the true duration. Over `MaxDurationSeconds` → files deleted,
   validation error. An artwork failure at step 4 also deletes the audio already written: no path
   leaves a half-created job behind.

6. **[`FileSystemJobStore.EnqueueAsync`](../backend/src/Infrastructure/Jobs/FileSystemJobStore.cs)**
   Writes `status.json`. That write *is* the enqueue. The controller returns 201 with the job id.

7. **[`JobPollingService`](../backend/src/Worker.Hosting/JobPollingService.cs)** — separate
   container, or the same process. `TryDequeueNextAsync` claims the job, `ProcessRenderJobUseCase`
   drives `FfmpegVideoRenderer`, and progress is written back into `status.json` as it goes. That
   file is what the SPA's polling of `GET /jobs/{id}` reads.

## Why rendering is four ffmpeg passes

The one genuinely non-obvious piece, in
[`FfmpegVideoRenderer`](../backend/src/Infrastructure/Rendering/FfmpegVideoRenderer.cs):

1. Circular-cropped, white-bordered artwork → **one static PNG**. The per-pixel `geq` mask runs
   once rather than on every frame.
2. Blurred, darkened artwork → **one static PNG** used as an ambient background.
3. **Exactly one rotation period** of the disc spinning over that background with the caption —
   roughly 60 frames. The rotation is perfectly periodic, so these are the only visually unique
   frames that exist.
4. `-stream_loop -1` that clip to the audio's real duration with **`-c:v copy`** — the encoded
   bytes are repackaged, not re-encoded.

Video encoding is reduced to one rotation. Audio processing, muxing, output size and transfer still grow with duration. The rotation period is snapped to a whole number of frames
(`FfmpegArgumentsBuilder.SnapRotationPeriodToFrames`) so the loop wraps with no visible jump.

## Configuration and errors

Configuration is environment variables, parsed by
[`JobsOptionsFactory`](../backend/src/Api/Configuration/JobsOptionsFactory.cs), which is pure and
total: a malformed value logs a warning and falls back to the documented default rather than
aborting startup. Reads are deferred into delegates so they see the fully merged configuration —
the integration tests depend on that, and one of them fails if it regresses.

Errors cross the boundary as `Result<T>` carrying an `ErrorCodes` value, mapped to `ProblemDetails`
responses by `JobsController.MapError`. Renderer diagnostics never reach the client; see
[SECURITY.md](SECURITY.md), finding F-2.

The upload limits have a single source of truth: `GET /limits` publishes the instance's effective
values and the SPA renders its hints and pre-checks from them, because the demo's limits are far
smaller than a self-hosted instance's and a hardcoded frontend would promise uploads the server
rejects.

## Enforced single-instance and media access contract

`FileSystemInstanceLease` holds an exclusive OS handle per API/worker role. `InstanceLeaseService` acquires it before workers start. A second process using the same jobs directory fails startup. This is local filesystem coordination, not a distributed lock.

`JobDownloadGate` serializes download admission through persistence; transfer occurs after releasing it. Writes use unique temp files. Readers permit atomic rename on Windows. `/jobs/{id}/preview` supports ranges without incrementing saves; `/download` serves an attachment and increments saves. Both reserve a whole file per request against process-local egress, conservatively overcounting range responses and aborted transfers.

`scripts/architecture.mjs` enforces dependency direction in `npm run verify`. One worker owns processing updates. Cleanup can race with expiring file transfers; ephemeral storage loss on host restart remains a demo limitation.
