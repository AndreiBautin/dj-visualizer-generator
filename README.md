# Spinner — DJ Visualizer

Turn audio and cover art into a spinning-record MP4. Built with .NET 9, React, TypeScript and FFmpeg, with asynchronous rendering, progress, preview and download. No login is required; possession of a job URL grants access until its files expire.

**Demo status (2026-09-21):** the [hosted demo](https://dj-visualizer.onrender.com) is suspended. Local startup works independently; do not share the hosted link as a working demo until the release checklist is verified.

## Run

Install [Node.js 24](https://nodejs.org), [.NET 9 SDK](https://dotnet.microsoft.com/download), and [FFmpeg/ffprobe](https://ffmpeg.org/download.html), available on PATH. On Windows, double-click **start-app.bat**. It installs locked dependencies, builds both parts, checks port 5080, starts the server, waits for health, and opens http://localhost:5080. It prints the server PID and log path for stopping/troubleshooting.

Choose **Render a sample mix** to exercise the real pipeline using bundled synthesized audio and generated artwork. Alternatively upload MP3/WAV/FLAC/M4A and PNG/JPG; select 720p/1080p, rotation speed and caption font. Limits come from the server. Public-host settings differ from local defaults.

For Linux/container startup, use `docker compose up --build` (UI 5173, API 5080). See [deployment](docs/DEPLOYMENT.md).

## Verify

```sh
npm ci --prefix frontend
npm run prepare
npm run verify
```

The gate checks architecture boundaries, lint, formatting, backend tests including real media execution, frontend tests, TypeScript and both frontend build modes. A skipped backend test fails it. CI also audits dependencies, scans history, builds the container variants and runs browser end-to-end tests.

## The engineering decision

The animation is periodic. Encode one rotation, then loop the encoded clip with stream copy instead of encoding every repeated frame. This reduces video encoding work; audio processing, muxing, output size and transfer still grow with mix length. Avoid claiming constant total render time.

A persisted `status.json` is both the job record and queue entry. Domain owns valid states, Application orchestrates, and Infrastructure handles files and FFmpeg. One API and one worker per jobs directory are enforced with OS file leases. It is a bounded single-instance design, not a distributed queue.

- [Architecture and request trace](docs/ARCHITECTURE.md)
- [Security and limitations](docs/SECURITY.md)
- [Tests and deliberate exclusions](docs/TESTING.md)
- [Demo data](docs/DEMO_DATA.md)
- [Interview guide](docs/INTERVIEW_GUIDE.md)
- [Readiness and verification record](docs/PORTFOLIO_DEMO_READINESS.md)

The existing Python mastering/AI experiment is outside this app's demonstrated functionality.

