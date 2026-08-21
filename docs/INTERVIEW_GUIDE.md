# Interview guide

Written to be said out loud. Every claim here is true of the code in this repository — if
something below sounds impressive but you cannot point at the file, it should not be here.

## The 30-second version

> It turns a DJ mix and a piece of cover art into a video of a spinning record with the title
> underneath, so a set can be posted somewhere that needs a video rather than an audio file.
>
> The interesting part isn't the visual, it's the render cost. The naive version encodes every
> frame, so a three-hour set takes hours. But the record spins at a constant rate, so the video is
> perfectly periodic — after one rotation you're generating frames you've already generated. So it
> renders exactly one rotation, about sixty frames, and then loops that clip to the length of the
> audio with a stream copy, which repackages bytes instead of re-encoding them. Render time is
> basically independent of mix length. A six-hour set costs about what a five-minute one does.
>
> It's .NET 9 in Clean Architecture with a React frontend, running as a single free container on
> Render.

> **Before using this line:** it assumes the Render deploy has actually been done (see
> [DEPLOYMENT.md](DEPLOYMENT.md#deploying) — about five minutes). Until then say "it's packaged to
> deploy as a single container" instead. Do not claim a live site you cannot open in front of
> them.

Then stop. Let them pick the thread.

## Explaining the architecture — lead with these three

1. **Clean Architecture, enforced literally.** The Domain project has zero package references.
   Application depends only on Domain and declares the interfaces it needs. Infrastructure
   implements them. Api and Worker are composition roots with no business logic.
2. **No database, no broker, no auth — all three on purpose.** A job is a `status.json` in its own
   directory. `FileSystemJobStore` implements both `IJobRepository` and `IJobQueue`.
3. **The same worker code runs two ways.** Separate container in docker-compose; in-process inside
   the API for single-instance free hosting. One flag, not a fork.

Say the absences early. Interviewers expect a database, and "there isn't one, here's why" is a
stronger opening than letting them find out and wonder if you forgot.

## Request lifecycle, naming real files

Walk `POST /jobs`:

1. `Api/Controllers/JobsController.cs` — binds the multipart form, rate-limits at 10/min per IP,
   opens both file streams. Touches no disk itself.
2. `Application/Jobs/CreateJobUseCase.cs` — checks free disk, then builds domain values.
3. `Domain/Jobs/JobTitle.cs`, `VideoPreset.cs`, `RotationSpeed.cs` — **validation lives here**, in
   the constructors. Each throws its own exception; the use case maps them to a validation error.
4. `Infrastructure/Uploads/FileSystemJobFileStorage.cs` — extension allowlist, then 16 magic bytes
   against `FileSignatureValidator`, then streams to disk counting bytes and aborting past the
   limit. Never trusts `Content-Length`.
5. `Infrastructure/Audio/FfmpegAudioProbe.cs` — real duration via ffprobe; over the limit means the
   files are deleted and the request rejected.
6. `Infrastructure/Jobs/FileSystemJobStore.cs` — writes `status.json`. **That write is the
   enqueue.** 201 with the job id.
7. `Worker.Hosting/JobPollingService.cs` — claims the oldest queued job by transitioning it to
   Processing and persisting *before* returning it, then renders, writing progress back into the
   same file the client polls.

The line worth landing: **"the write is the enqueue."** It's the whole design in four words.

## Engineering decisions

Each as: decision → alternatives → why → **what it costs**.

**Filesystem as queue and repository.** Alternatives: Postgres + a jobs table; Redis; RabbitMQ.
Chose the filesystem because the domain is one short-lived entity with a four-state lifecycle, no
relationships and no queries, and the workers already need a shared filesystem for the media
itself — so a database would be a second stateful thing to run for no gain.
**Cost:** it only works with one worker. `TryDequeueNextAsync`'s claim is a read-then-write that
two workers could interleave. Scaling out means real locking or a real queue. It's documented, not
hidden.

**Four ffmpeg passes instead of one.** Alternatives: single filter graph (obvious); GPU encoding.
Chose the split because profiling a real 3-hour mix showed ~5 hours of render, and the causes were
structural: the per-pixel `geq` crop mask was recomputed every frame, and every frame was encoded
even though the rotation repeats every 60. Statics are rendered once, one rotation period is
encoded, and the rest is `-c:v copy`.
**Cost:** four processes to orchestrate and clean up instead of one, intermediate files to delete
in a `finally`, and the loop period must be snapped to a whole number of frames or the wrap is
visible. More moving parts for a large constant factor — worth it here, not always.

**NVENC implemented but not enabled.** It's behind `Worker__VideoCodec`. Tried it; this ffmpeg
build wanted NVENC API 13.1 and the installed driver offered 13.0. Left the code path in and the
default on libx264 rather than pushing a driver update.
**Cost:** a config path that isn't exercised by default. Worth saying out loud — it shows you'll
leave something off rather than ship an untested default. It also stopped being the important
lever once the render no longer scales with duration.

**Caption via `textfile=` rather than inline `text=`.** See the security section — this is the
best story in the project.

**Worker hosted services extracted into a library.** Alternatives: duplicate the wiring in the
API; have the API reference the Worker executable. Referencing the executable actually failed —
both entry points ship an `appsettings.json` and they collided on publish. A library has none, and
it's the honest shape anyway: the API wants the services, not a second entry point.
**Cost:** one more project in the solution.

**Server-published upload limits.** The SPA used to hardcode 2 GB and 25 MB. On the free tier the
real limits are 60 MB and 15 minutes, so the UI would have invited uploads the server rejects.
`GET /limits` publishes them and the UI renders from that.
**Cost:** an extra request on load, and a fallback path when it fails. Cheap next to a demo that
lies to the person trying it.

## Security talking points — lead with the threat model

> There's no auth, no database, no cookies and no sessions. That structurally removes SQL
> injection, CSRF, session attacks and IDOR — not "mitigated", *absent*. What's actually exposed is
> two file-upload boundaries feeding ffmpeg, a caption that reaches an ffmpeg filter graph, and a
> job id used to build filesystem paths.

Naming what's structurally absent is stronger than a checklist of N/As.

Then the finding, which is the best thing you have to talk about:

> The job title was interpolated into the ffmpeg filter graph inside a single-quoted `drawtext`
> option, with hand-rolled escaping for backslash, colon, quote and percent. There were two unit
> tests asserting the escaping happened, and they passed.
>
> The escaping was wrong. ffmpeg doesn't honour a backslash-escaped quote inside a single-quoted
> option — the quote just terminates it. So any title with an apostrophe broke out of its own
> option and injected into the graph. Which is a security problem, because ffmpeg's filter language
> can read files, and also just a plain bug, because DJ set titles are full of apostrophes.
>
> I found it by rendering with hostile titles for real instead of asserting on the string. Three of
> six failed immediately.
>
> The fix wasn't better escaping — it was removing the escaping problem. The caption goes to a temp
> file and drawtext reads it with `textfile=` and `expansion=none`. User bytes never enter the
> graph string, so there's no escaping rule left to get wrong. The test now renders with real
> ffmpeg for six hostile captions.

The reusable point: **a unit test that asserts you escaped something proves nothing about whether
the escaping is correct — only the thing doing the parsing can tell you that.**

Second finding, briefly: ffmpeg's stderr — full command line, absolute paths, the whole filter
graph — was stored on the job and returned by the status endpoint. Now the exception is logged and
the caller gets one of two fixed messages.

## Data model

There is no schema. A job is:

```
jobs-data/<guid>/
  status.json          id, title, preset, rotation speed, font, status, progress, error, timestamps
  input/audio.<ext>    stored under a fixed name — the uploaded filename is discarded
  input/artwork.<ext>
  output/video.mp4
```

- **Indexes:** none. Lookup is by GUID, which is a directory name — the filesystem is the index.
- **Relationships:** none. One entity.
- **Migrations:** none. `JobDto` is the serialization contract; a new optional field is a nullable
  property.
- **Access:** `FileSystemJobStore`, behind `IJobRepository` and `IJobQueue`.

**What breaks at scale:** `GetAllAsync` — used by the cleanup sweep — enumerates every job
directory and deserializes every `status.json`. That's fine at hundreds and bad at hundreds of
thousands. And dequeue scans to find the oldest queued job. Both are O(n) per sweep. The first
thing that would move to Postgres is the job index, keeping the media on disk or in object storage.

Have this ready — "what breaks at scale" is where a filesystem-as-database answer gets probed, and
knowing the answer precisely is what makes the original choice read as a decision rather than an
oversight.

## Deployment

One container on Render's free tier: 512 MB, ~0.1 CPU, ephemeral disk, spins down after 15 minutes.

> The free tier gives you one web service, no persistent disk, and no background-worker type. The
> app is normally three containers sharing a volume. Rather than fork it, I made the worker's
> hosted services a library that either composition root can register, so `Jobs__SingleContainer=true`
> runs the same code in the API process and serves the SPA from the same origin. docker-compose
> still runs the real three-container architecture.

Why Render: Fly.io ended its free tier, Railway is trial credit, Koyeb dropped free compute, and
Hugging Face now requires a paid plan to create a Docker Space. GCP/Oracle/Azure want a card. That
left one option — and being able to say *why the others were rejected* is the answer, not "I used
Render."

CI: build, test, lint, typecheck, dependency audit gated at high, gitleaks over full history, all
four images built, and a smoke test that runs the deployed image and drives a sample render end to
end. **CI and deploy run in parallel rather than gated** — an accepted trade-off, and the one-line
change to gate it is in DEPLOYMENT.md. Say that before they ask; an acknowledged trade-off reads as
judgement, an unmentioned one reads as an oversight.

## Testing

311 tests, up from 250 — **and CI is green, which it had never been.** Every run before this work
failed, for three separate reasons that all passed locally. That's worth volunteering:

> The suite was green on my machine and red in CI the whole time. Three causes.
> `Path.GetInvalidFileNameChars()` returns forty-odd characters on Windows and two on Linux, so
> download filenames differed between my machine and the container it deploys to — and the
> deployed behaviour was the untested one. The drawtext escaping bug. And a Playwright test that
> could never pass, because clicking a range input moves the thumb to where you clicked, so the
> assertion about the value afterwards was checking a number that was never going to be there.
>
> The common thread is that all three tests asserted what my code did rather than what the system
> actually accepted.

The ones worth naming:

- Six hostile captions rendered with real ffmpeg (the F-1 fix).
- The error message must contain no server path and not even the word "ffmpeg".
- The demo fixtures are scanned byte-for-byte for anything personal, including checking that the
  MP3's ID3 tag holds only ffmpeg's encoder frame.
- Config parsing can't crash and can't read a typo as the opposite mode.

**Deliberately not tested:** the visual output (verified by extracting frames and looking),
ffmpeg itself, render timings (they'd be flaky), and concurrent workers (the design says one).

## Deliberate simplifications

| Not built | Why | What it would take |
|---|---|---|
| Accounts / auth | No per-user data to protect; adding it would mean sessions, storage and a login for a tool that needs none | Auth provider + ownership checks on job routes |
| Database | One entity, no relationships, no queries | Postgres + EF Core; job index first, media stays on disk |
| Horizontal scaling | Filesystem claim is single-worker-safe only | A real queue, or advisory locking |
| Object storage | Local disk is enough at this size | S3-compatible store behind `IJobFileStorage` |
| Resumable uploads | Multipart is fine at demo limits | tus or chunked upload |
| Email / notifications | Nobody to notify without accounts | — |
| Video preview before download | The download is the product | — |
| Metrics pipeline | Platform logs are enough at this size | — |

That table is the highest-signal thing in this document. Knowing precisely where you stopped, and
what it would cost to go further, reads better than a longer feature list.

## Likely questions

**"Isn't Clean Architecture over-engineered for this?"**
> For the feature set, arguably. What it bought concretely: the worker's hosted services depend
> only on interfaces, so registering them inside the API process for free-tier hosting was a
> composition change with no code change. And every use case is testable without a filesystem. I'd
> say the layering earned itself — but I wouldn't put a DI container and repositories on a CRUD
> form.

**"What's the weakest part?"**
> Single-worker dequeue. `TryDequeueNextAsync` claims a job with a read-then-write, and two workers
> could interleave and both take it. It's correct for how it's deployed and documented as an
> assumption, but it's the thing that breaks first if this ever needed to scale, and "documented"
> isn't the same as "safe."

**"What would you do differently?"**
> Two things. I'd have rendered with hostile captions on day one — the escaping bug lived under two
> passing tests for the whole project because I tested that escaping happened rather than that
> ffmpeg accepted it. And I'd have published the upload limits from the server from the start; I
> hardcoded them in the frontend, and the moment there was a second deployment with different
> limits, the UI was lying.

**"Why not just use ffmpeg's `-loop` in one command?"**
> That's what it did originally. The problem is the crop mask is a per-pixel expression, so it gets
> recomputed for every output frame even though the artwork never changes. Splitting the statics
> out was a ~3x win, and only encoding one rotation period was the one that mattered — it made
> render time independent of duration.

**"How do you know the demo has none of your own music in it?"**
> Because there's no step in the pipeline that could put it there. The fixture is generated by a
> committed ffmpeg script from sine waves and a drawn gradient — there's no export from my machine
> anywhere. And there's a test that scans both files for emails, URLs, paths and credential
> patterns, and checks the MP3's ID3 tag contains only ffmpeg's own encoder frame.

**"It's slow."**
> On the free tier, yes — a tenth of a CPU. That's why the demo caps at 15 minutes of audio and
> defaults to `ultrafast`. On real hardware, 30 minutes of 1080p rendered in 29 seconds.

## Things not to say

- **Don't say "production-ready."** Say what's deployed, tested and documented — and that it's a
  free-tier demo with an ephemeral disk and no auth.
- **Don't call it scalable.** It's explicitly single-worker. Say "it scales vertically and I know
  exactly what breaks first."
- **Don't claim the security work was comprehensive.** Two real findings were found and fixed;
  ffmpeg still parses untrusted media, and that's the biggest remaining risk.
- **Don't say the tests prove it works.** They prove behaviour. The visual output was verified by
  looking at extracted frames — say that, it's more credible.
- **Don't oversell the architecture.** "I applied Clean Architecture" invites "why?". "The Domain
  has no package references and here's what that bought me" doesn't.
- **Don't claim Docker images were verified locally.** They're built and smoke-tested in CI; WSL2
  isn't installed on the dev machine, so they were never built there.
