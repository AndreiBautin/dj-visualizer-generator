# DJ Visualizer Generator

Turns a DJ mix and a piece of cover art into a video of a spinning record with the title
underneath — so a set can go anywhere that wants a video instead of an audio file. No account, no
subscription, no duration cap.

## Live demo

**→ [dj-visualizer.onrender.com](https://dj-visualizer.onrender.com)**

**There is no login** — open it and use it. If you haven't got a mix to hand, click **"Render a
sample mix"**: the app renders a short synthesized track it ships with, so you can watch the whole
pipeline run without uploading anything.

Three things to expect, because it's a free instance:

- The first request after a quiet spell takes up to a minute while the container wakes up.
- Uploads are capped at 60 MB / 15 minutes (a self-hosted instance does 2 GB / 6 hours). The UI
  reads those limits from the server, so what it shows is always what it will accept.
- It runs on roughly a tenth of a CPU. The bundled 24-second sample renders in about **67 seconds**
  there, against 8 seconds on a normal machine.

## What it does

- **In:** MP3, WAV, FLAC or M4A, plus JPG or PNG artwork.
- **Out:** 1080p or 720p H.264/AAC MP4, exactly as long as the audio. Artwork cropped to a circle
  with a white border and a soft drop shadow, spinning over an ambient blurred glow of the
  artwork's own colours, title along the bottom.
- **Choices:** rotation speed (2–15 s per spin) and caption font (sans / serif / mono).
- **Nothing kept:** no accounts, no database. Files are deleted automatically after a short
  retention window.

## The one thing worth knowing

A naive renderer encodes every frame, so a three-hour set takes hours. But the record spins at a
constant rate, so the video is **perfectly periodic** — past one rotation you're re-generating
frames you already have.

So it renders exactly one rotation (~60 frames) and loops that clip to the audio's length with
`-c:v copy`, which repackages the encoded bytes instead of re-encoding them. The circular crop and
the blurred background are rendered once as static images rather than per frame.

**Render time is therefore near-independent of mix length.** 30 minutes of 1080p renders in about
29 seconds on a normal machine; a six-hour set costs about the same. Before this change, the same
input was tracking to over 20 minutes.

Details in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Architecture

```
React SPA ──HTTP──▶ Api ──▶ Application ──▶ Domain
                     │           ▲
                     │      Infrastructure (ffmpeg, filesystem)
                     ▼
            jobs-data/<id>/status.json   ← the queue AND the repository
                     ▲
                     └── Worker polls for Queued jobs
```

Clean Architecture, enforced literally: `Domain` has zero package references, `Application`
declares the interfaces it needs, `Infrastructure` implements them, and `Api`/`Worker` are
composition roots with no business logic.

**No database, no message broker, no auth** — all three deliberate. A job is a `status.json` in its
own directory, and `FileSystemJobStore` implements both `IJobRepository` and `IJobQueue` over it.
Writing that file *is* the enqueue.

Full walkthrough, including one request traced end to end through real files:
**[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

## Tech stack, and why

| | Why this one |
|---|---|
| **.NET 9 / ASP.NET Core** | Long-running CPU-bound work with real threading and first-class process control — this app's core job is orchestrating ffmpeg |
| **ffmpeg** | The only realistic option for this pipeline. Driven via argument lists, never a shell string |
| **React 19 + Vite + TypeScript** | The UI is one form and a progress poll; Vite keeps the bundle ~100 KB gzipped |
| **TanStack Query** | Job status is polled server state, which is exactly what it's for — no hand-rolled polling or cache |
| **React Hook Form + Zod** | One schema validates the form and types it; the Domain re-validates server-side regardless |
| **Tailwind v4** | Small UI, no design system needed |
| **xUnit + NSubstitute + FluentAssertions** | Standard .NET stack; assertions read as sentences in the failure output |
| **Vitest + Testing Library** | Tests user-visible behaviour rather than component internals |
| **Docker** | ffmpeg and the fonts are system dependencies — the image is the honest unit of deployment |
| **No database** | One short-lived entity, no relationships, no queries. It would be a thing to run and back up for no gain |

## Security

No accounts, no cookies, no database — which structurally removes SQL injection, CSRF, session
attacks and IDOR rather than mitigating them. What *is* exposed: two upload boundaries feeding
ffmpeg, and a caption that reaches an ffmpeg filter graph.

Two real vulnerabilities were found and fixed during productionization — a **drawtext filter-graph
injection** via the job title (which had shipped underneath two passing unit tests), and ffmpeg
diagnostics leaking server paths to callers. CI runs gitleaks over the full history and fails on
high-severity dependency vulnerabilities.

Threat model, both findings in detail, and the risks that remain:
**[docs/SECURITY.md](docs/SECURITY.md)**.

## Testing

**318 tests** — 254 backend (xUnit), 64 frontend (Vitest) — plus Playwright against the full
docker-compose stack in CI. ffmpeg-dependent tests run for real rather than skipping.

What's prioritised, and what's deliberately left untested:
**[docs/TESTING.md](docs/TESTING.md)**.

## Deployment

One container on Render's free tier — the API serves the SPA and hosts the render worker
in-process, because a free web service gets no persistent disk and no background-worker type.
`docker-compose.yml` still runs the real three-container architecture; single-container mode is a
config flag, not a fork.

Which providers were rejected and why, environment variables, and a troubleshooting table:
**[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)**.

## Running it locally

**With Docker** — the full three-container stack:

```bash
docker compose up --build
```

Frontend on http://localhost:5173, API on http://localhost:5080.

**Without Docker** — needs .NET 9 SDK, Node 24, and ffmpeg on `PATH`:

```bash
./run.bat
```

On Windows this starts the API and Worker with a shared jobs directory and the right font paths,
then the Vite dev server. Otherwise run the three by hand:

```bash
dotnet run --project backend/src/Api --urls http://localhost:5080
```

```bash
dotnet run --project backend/src/Worker
```

```bash
npm --prefix frontend install && npm --prefix frontend run dev
```

Set `Jobs__RootPath` to the same absolute path for both .NET processes — they communicate through
that directory, and their defaults differ.

**Configuration:** copy `.env.example` to `.env`. Every variable is documented there, including
which `VITE_`-prefixed values get compiled into the public bundle.

**Regenerating the demo assets:**

```bash
bash scripts/generate-demo-assets.sh
```

## Documentation

| | |
|---|---|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layers, the three deliberate absences, one request traced end to end |
| [SECURITY.md](docs/SECURITY.md) | Threat model, findings and fixes, remaining risks |
| [DEMO_DATA.md](docs/DEMO_DATA.md) | How the sample mix is generated and why it can't contain anything personal |
| [DEPLOYMENT.md](docs/DEPLOYMENT.md) | Provider comparison, env vars, troubleshooting |
| [TESTING.md](docs/TESTING.md) | Strategy per layer, and what is deliberately not tested |
| [PRODUCTIONIZATION_ASSESSMENT.md](docs/PRODUCTIONIZATION_ASSESSMENT.md) | The state this repo was in before deployment work, assessed honestly |

## Licence

MIT — see [LICENSE](LICENSE). Bundled fonts (Poppins, Abril Fatface, Space Mono) are OFL-licensed;
their licences are in `assets/fonts/`.
