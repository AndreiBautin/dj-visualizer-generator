# Productionization assessment

Assessed 2026-08-20 against commit `c4fa400`. Baseline measured before any change:
**197 backend tests + 53 frontend tests, all passing**; `dotnet build -c Release`, `npm run build`
and `oxlint` all clean.

## Verdict up front

This codebase did not need productionizing in the usual sense. It needed **deploying**.

The application is already well built: Clean Architecture with a Domain project that has zero
package references, an Application layer that defines its own interfaces, Infrastructure
implementing them, and two thin composition roots. Uploads are validated by extension allowlist
*and* magic-byte signature, size limits are enforced while streaming rather than trusting
`Content-Length`, job IDs are strictly GUID-parsed, path construction is re-validated against the
jobs root, ffmpeg is invoked with an argument **list** rather than a shell string, and job
creation is rate-limited. The render pipeline is genuinely clever — rendering one rotation period
and looping it makes render cost independent of mix length.

So this assessment does not propose a rewrite. It fixes two real security defects, closes the
gap that actually blocks a reviewer (no deployment, no demo path), and documents the rest.

## Current architecture

```
frontend (React 19 + Vite + Tailwind)  ──HTTP──▶  Api (ASP.NET Core 9)
                                                     │ writes
                                                     ▼
                                          jobs-data/<job-id>/   ◀── shared filesystem
                                             status.json            (queue + repository)
                                             input/ output/
                                                     ▲ polls
                                                     │
                                                  Worker (.NET generic host + ffmpeg)
```

No database, no message broker, no auth — all three deliberate. Job state is a `status.json` per
job directory; `FileSystemJobStore` implements both `IJobRepository` and `IJobQueue`, and
`TryDequeueNextAsync` atomically claims the oldest queued job by transitioning it to `Processing`
before returning it.

## Weaknesses

| # | Finding | Impact |
|---|---------|--------|
| 1 | Nothing is deployed; the GitHub repo is private | Fatal for the stated purpose. An employer cannot click anything. |
| 2 | Job caption inlined into an ffmpeg filter graph (see Security F-1) | High — filter-graph injection; also broke every title containing an apostrophe |
| 3 | Raw ffmpeg/ffprobe stderr returned to the client (see Security F-2) | Medium — discloses absolute server paths and internal filter graph |
| 4 | No demo path — a reviewer must supply their own audio and artwork | High. A portfolio app that demands a 2 GB upload before it shows anything demonstrates nothing. |
| 5 | Upload limits (2 GB audio, 6 h duration) assume a real machine | Blocks free hosting; 512 MB / 0.1 CPU cannot honour them |
| 6 | Three containers sharing a volume | Blocks free hosting; free tiers give one web service and no persistent disk |
| 7 | CI has no dependency audit and no secret scan | Vulnerable dependency or a committed key would pass unnoticed |
| 8 | CI never deploys and never checks a live URL | Green CI would coexist with a dead site |
| 9 | No `.gitattributes`; author on Windows, CI on Linux | A formatting gate would pass in CI and fail locally |
| 10 | `SecurityHeadersMiddleware` sends `default-src 'none'` on every response | Correct for a JSON API; would break the SPA outright once the API also serves it |

## Security findings

Full detail, including the threat model and what is structurally absent, is in
[SECURITY.md](SECURITY.md). Summary:

| ID | Severity | Finding | Status |
|----|----------|---------|--------|
| F-1 | **High** | drawtext filter-graph injection via job title | Fixed — caption now passed by `textfile=` with `expansion=none` |
| F-2 | Medium | ffmpeg diagnostics leaked to the client via `job.errorMessage` | Fixed — safe message to caller, detail to logs |
| F-3 | Low | `AllowedHosts: "*"` | Accepted; documented |
| F-4 | Low | No CSP on SPA responses | Fixed as part of single-container serving |

No secrets were found in the working tree or in the full git history (`git grep` over
`git rev-list --all`: every hit was the word `CancellationToken`). `jobs-data/` is empty and
gitignored. The local `api.log` / `worker.log` contain job IDs only — no user content — which
means the app's logging was already safe to leave on.

## Data and privacy

The app stores no personal data of the operator's: a job is an uploaded file pair plus a
`status.json`, deleted 60 minutes after completion by `CleanupService`. The privacy risk is not
leakage of existing data but **the demo becoming a free public transcoder**. That shapes the demo
design: small limits, short retention, and a rate limiter that is already present.

Nothing personal can reach the deployed app by accident, because there is no export step from
this machine anywhere in the pipeline — the sample assets are generated by ffmpeg from synthesized
tones and a drawn gradient. See [DEMO_DATA.md](DEMO_DATA.md).

## Recommended deployment

One container on Render's free web-service tier. Rejected alternatives and the reasoning are in
[DEPLOYMENT.md](DEPLOYMENT.md); the short version is that Fly.io, Railway, Koyeb and Hugging Face
Docker Spaces all now require either a credit card or a paid plan, verified 2026-08-20.

The three-container `docker-compose.yml` stays exactly as it is — it remains the honest
architecture. The single-container image is an additional composition root wiring, not a fork:
the Worker's hosted services are registered inside the API process when
`Worker__RunInProcess=true`.

## Major risks

1. **0.1 CPU / 512 MB.** Render's free instance is roughly a tenth of a core. Render times must
   be measured on the deployed instance, not extrapolated from this machine.
2. **Ephemeral disk + 15-minute spin-down.** A job in flight when the instance sleeps is lost.
   Acceptable given a 60-minute retention window, but it must be stated in the UI, not discovered.
3. **The repo is private.** Making it public is the user's decision and cannot be automated here.

## Implementation order

1. Fix F-1 and F-2 (done first — they are correctness bugs as much as security bugs)
2. Generated demo assets + a one-click demo path
3. Configuration profile for constrained hosting
4. Single-container image; API serves the SPA and hosts the worker
5. Deploy to Render, measure real render time
6. CI: dependency audit, secret scan, deploy, live smoke test
7. Documentation and interview guide
