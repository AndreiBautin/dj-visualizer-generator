# DJ Visualizer Generator

A free, open-source tool that turns a DJ mix (audio, up to 6 hours) and a piece of artwork into
a professional-looking spinning-record video, ready to upload to YouTube or SoundCloud. No
subscriptions, no duration caps, no account required.

- **Upload:** MP3, WAV, FLAC, or M4A audio (up to 2 GB) + JPG or PNG artwork (up to 25 MB).
- **Output:** 1920x1080 or 1280x720 MP4, H.264/30fps, artwork cropped to a circle with a white
  border with a soft drop shadow, rotating continuously over an ambient blurred glow of the
  artwork's own colors, with the track title overlaid bottom-center.
  Video duration exactly matches the input audio.
- **Customizable, with sane defaults:** rotation speed (2-15s per spin, default 3s) and caption
  font (Sans/Serif/Mono) are optional per-render choices in the upload form.
- **No database, no accounts.** Job state lives on disk next to the files themselves; files are
  deleted automatically after a short retention window.

## Architecture

```
Browser (React + Vite SPA)
   │  multipart upload (audio + artwork + title + preset) → POST /jobs
   │  poll → GET /jobs/{id}          download → GET /jobs/{id}/download
   ▼
ASP.NET Core API  (Clean Architecture: Api → Application → Domain ← Infrastructure)
   │  writes job folder to a shared volume: /data/jobs/{jobId}/
   │    input/audio.<ext>, input/artwork.<ext>, status.json, output/video.mp4
   ▼
Shared filesystem (Docker volume; a local folder in dev)
   ▲
   │  polls the same folder for status=Queued every few seconds
Worker (.NET Worker Service — reuses Domain/Application/Infrastructure)
   │  runs ffmpeg (circular crop, border+shadow, ambient background, rotation, drawtext, H.264 encode)
   │  writes output/video.mp4, updates status.json progress 0-100
   ▼
output/video.mp4 — served by the API, deleted by the Worker's cleanup sweep after retention
```

There is intentionally no message broker and no database. `IJobRepository` (lookup/save/list) and
`IJobQueue` (enqueue/atomically-claim-next) are the two seams that would need new Infrastructure
implementations to scale beyond one Worker instance — everything above those interfaces is
unaware of the filesystem-backed implementation.

### Solution layout

```
backend/
  src/
    Domain/           entities, value objects, business rules — zero dependencies
    Application/       use cases, interfaces (ports), DTOs — depends only on Domain
    Infrastructure/     filesystem job store, ffmpeg process management, file validation
    Api/                controllers, middleware, DI composition root
    Worker/             background services (polling, cleanup), DI composition root
  tests/               one xUnit project per src project, mirroring its structure
frontend/
  src/
    api/                typed fetch client
    components/         UploadCard, FilePreview, RenderSettings, ProgressPanel, DownloadPanel
    hooks/               useJobStatus (React Query polling)
    schemas/            Zod validation
    lib/                 pure helpers (file validation, formatting)
  e2e/                  Playwright happy-path test
assets/fonts/           bundled OFL-licensed caption fonts (see the *-OFL.txt files)
docker/                 Dockerfiles for api, worker, frontend (+ nginx config)
```

## Local setup (without Docker)

**Windows: just run [`run.bat`](run.bat)** from the repo root. It starts the API, Worker, and
frontend each in their own window, points them at a shared job storage folder, points ffmpeg's
title overlay at the bundled fonts in `assets/fonts/`, and opens your browser. Requires .NET 9
SDK, Node.js 24+, and
`ffmpeg`/`ffprobe` on PATH (install via `winget install Gyan.FFmpeg` if you don't have them —
actual rendering needs them; the rest of the app works without them, failing renders with a clear
error instead).

To run the three services by hand (any OS):

```bash
# Backend API (http://localhost:5080)
Jobs__RootPath=/path/to/shared/jobs-data dotnet run --project backend/src/Api --urls http://localhost:5080

# Worker (separate terminal — MUST use the same Jobs__RootPath as the API, or it never sees
# jobs the API creates; on Windows also set Worker__FontFilePathSansBold/SerifBold/MonoBold to
# the bundled fonts in assets/fonts/, since the production defaults are Linux container paths
# copied in by Dockerfile.worker - run.bat does this automatically)
Jobs__RootPath=/path/to/shared/jobs-data dotnet run --project backend/src/Worker

# Frontend (http://localhost:5173, proxies /api to the API above)
cd frontend && npm install && npm run dev
```

Without an explicit `Jobs__RootPath`, the API and Worker each fall back to a `jobs-data/` folder
next to their *own* build output — two different folders — and the Worker will never see jobs the
API creates. Always set it explicitly to the same path for both processes in dev mode.

## Running with Docker

```bash
cp .env.example .env   # adjust limits if you want
docker compose up --build
```

- Frontend: http://localhost:5173
- API: http://localhost:5080 (health check at `/health`)

`docker-compose.yml` wires api + worker + frontend together with a shared `jobs-data` volume; the
frontend's nginx config proxies `/api/*` to the API container. `ffmpeg` is installed in both the
API image (needed for `ffprobe` duration checks at upload time) and the Worker image (needed for
the actual render); the Worker image also bundles the caption fonts from `assets/fonts/`.

## Testing

**Backend** (from `backend/`):

```bash
dotnet test
```

197 tests across Domain, Application, Infrastructure, Worker, and Api.IntegrationTests. A handful
of Infrastructure tests that actually invoke `ffmpeg`/`ffprobe` are tagged with a custom
`[RequiresFfmpegFact]` attribute and auto-skip on machines without ffmpeg installed — they run for
real in CI (which installs `ffmpeg` + `fonts-dejavu-core` via apt).

**Frontend** (from `frontend/`):

```bash
npm run test    # Vitest — component and unit tests
npm run lint     # oxlint
npm run build    # tsc + production build
```

53 tests across schemas, components, the API client, and `App`.

**End-to-end** (from `frontend/`, requires the full stack running — see `docker-compose.yml` or
the local setup above, and Playwright browsers installed via `npx playwright install`):

```bash
npx playwright test
```

The E2E test generates a tiny synthetic WAV + PNG at runtime (no binary fixtures committed),
uploads them through the real UI, and waits for a real render to complete and download. It runs in
its own CI job against the docker-compose stack, separately from the fast unit-test jobs.

## Deployment

The three services are independent containers with no shared code beyond their Dockerfiles' build
context — deploy `docker/Dockerfile.api` and `docker/Dockerfile.worker` to any container host
(the Worker needs no public networking, only filesystem access to the same volume as the API), and
`docker/Dockerfile.frontend` (a static build behind nginx) to any static/container host. They must
share persistent storage at the path referenced by `Jobs__RootPath` — a Docker volume, an NFS
mount, or equivalent, depending on your host. Configure via environment variables (see
`.env.example`): upload limits, minimum free disk space, and the jobs root path.

## Configuration reference

Per-render options (rotation speed, caption font) are set by the **user, per job**, in the upload
form — not server config. Everything below is server/worker-level configuration instead.

| Variable | Default | Applies to |
|---|---|---|
| `Jobs__RootPath` | `jobs-data/` next to the executable | Api, Worker |
| `Jobs__MaxAudioBytes` | 2 GB | Api |
| `Jobs__MaxImageBytes` | 25 MB | Api |
| `Jobs__MaxDurationSeconds` | 21600 (6 hours) | Api |
| `Jobs__MinFreeDiskBytes` | 3 GB | Api, Worker |
| `Worker__PollingIntervalSeconds` | 3 | Worker |
| `Worker__CleanupIntervalSeconds` | 300 | Worker |
| `Worker__RetentionMinutes` | 60 | Worker |
| `Worker__StaleProcessingMinutes` | 60 | Worker |
| `Worker__VideoCodec` | `libx264` (`h264_nvenc` opt-in for a compatible NVIDIA GPU) | Worker |
| `Worker__X264Preset` | `veryfast` (only used when `VideoCodec` is `libx264`) | Worker |
| `Worker__FontFilePathSansBold` | Poppins ExtraBold (`assets/fonts/`) | Worker |
| `Worker__FontFilePathSerifBold` | Abril Fatface (`assets/fonts/`) | Worker |
| `Worker__FontFilePathMonoBold` | Space Mono Bold (`assets/fonts/`) | Worker |

**Rotation speed** is user-selectable between `RotationSpeed.MinSecondsPerRotation` (2s) and
`MaxSecondsPerRotation` (15s) per rotation, defaulting to 3s. Whatever value is requested is snapped
to the nearest whole video frame (`FfmpegArgumentsBuilder.SnapRotationPeriodToFrames`) so the
looped render always wraps seamlessly, with no visible jump.

## Contributing

1. Fork and branch from `main`.
2. Follow TDD: write a failing test, confirm it fails for the right reason, implement the minimum
   to pass it, refactor, re-run the full suite.
3. Keep the layering intact — Domain has zero dependencies; Application depends only on Domain and
   defines interfaces that Infrastructure implements; Api/Worker are composition roots.
4. Run `dotnet test` and `npm run test && npm run lint && npm run build` before opening a PR; CI
   runs the same checks plus Docker image builds and the E2E suite.
5. Keep the scope tight — see "Important" in the original design brief: no auth, no database, no
   payments, no user accounts. This is a small, focused tool by design.

## Known limitations / residual risk

- **Single Worker instance.** Job claiming (`TryDequeueNextAsync`) is safe for one Worker process
  but not designed for multiple concurrent Worker instances racing to claim the same job — see
  "Architecture" above for the interfaces (`IJobRepository`, `IJobQueue`) that would need a real
  backing store (e.g. a database with row locking, or Redis) to support that.
- **NVENC hardware encoding is opt-in and machine-dependent.** `Worker__VideoCodec=h264_nvenc`
  needs a compatible NVIDIA GPU and a recent enough driver on the host actually running the
  Worker; the default `libx264` software encoding works everywhere, including Docker/CI with no
  GPU, and is what's exercised by CI.
- **Very large / long-running transfers need generous stall timeouts, not just size limits.**
  Kestrel and nginx both default to resetting a connection after ~60s of *no data movement* (not
  overall duration), which a multi-GB upload or multi-hour video download can trip if disk I/O or
  the network stalls even briefly. Both are configured generously for this app's use case (see
  `Program.cs`'s `MinRequestBodyDataRate`/`MinResponseDataRate` and `docker/nginx.conf`'s
  `client_body_timeout`/`send_timeout`) — worth revisiting if you deploy behind another proxy
  (a CDN, a corporate load balancer) that imposes its own defaults.

## License

[MIT](LICENSE)
