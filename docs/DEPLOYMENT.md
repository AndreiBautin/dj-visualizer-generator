# Deployment

## The constraint that decided everything

This app is not a static site and not a CRUD API. It is a **CPU-bound native media pipeline**:
it needs ffmpeg on the box, a writable disk for multi-hundred-megabyte uploads, and a long-running
background process. That rules out the entire "free static hosting" tier before the conversation
starts, and it makes the free container tiers the only candidates.

## Provider comparison

Verified 2026-08-20. Providers change these terms often — re-check before relying on this table.

| Provider | Free compute? | Card required? | Verdict |
|---|---|---|---|
| **Render** | Yes — free web service, 512 MB / 0.1 CPU, 750 instance-hours per month | **No**, for the free path | **Chosen** |
| Fly.io | No. Free tier discontinued; new users get a trial capped at 2 VM-hours or 7 days | Yes | Rejected — not free |
| Railway | Trial credit only ($5 first month, then $1/month), which buys hours, not a month | Effectively yes to continue | Rejected — runs out |
| Koyeb | No compute free tier remaining; the pricing page lists only a metered free Postgres allowance | — | Rejected |
| Hugging Face Spaces | CPU Basic hardware costs nothing, **but creating a Docker or Gradio Space requires a paid plan** (PRO for personal accounts). Only Static Spaces are free. | — | Rejected — would need PRO |
| Google Cloud Run / Oracle Cloud / Azure | Genuinely generous free tiers | **Yes** — card required at signup | Rejected on the no-card rule |
| GitHub Pages / Cloudflare Pages / Netlify | Static only | No | Rejected — cannot run ffmpeg or .NET |

Render was the only option that is actually free, actually runs a container, and actually does not
ask for a card.

## What Render's free tier costs us

Free is not free of consequences, and these are stated in the UI rather than discovered:

| Limit | Consequence |
|---|---|
| 512 MB RAM, ~0.1 CPU | Renders are far slower than on a real machine. Hence `Worker__X264Preset=ultrafast` and small upload limits. |
| **No persistent disk** | Jobs do not survive a restart. Acceptable: retention is 30 minutes anyway and the video is downloaded immediately. |
| **No background-worker service type** | The worker cannot be its own service. This is *the* reason single-container mode exists. |
| Spins down after 15 minutes idle | First visit after a quiet period waits ~1 minute for a cold start. |
| 750 instance-hours/month | One always-on service fits; spin-down makes overrun unlikely. |

## The deployed architecture

```
                    ┌──────────────────────────────────────┐
   visitor ────────▶│  Render free web service             │
                    │  ┌────────────────────────────────┐  │
                    │  │ Api process (Kestrel, :$PORT)  │  │
                    │  │  • serves the SPA from wwwroot │  │
                    │  │  • /jobs, /jobs/sample, /limits│  │
                    │  │  • JobPollingService  ─┐       │  │
                    │  │  • CleanupService      │       │  │
                    │  └────────────────────────┼───────┘  │
                    │            ffmpeg  ◀──────┘          │
                    │            /tmp/djvisualizer-jobs    │
                    │            (ephemeral)               │
                    └──────────────────────────────────────┘
```

One container, built from [`docker/Dockerfile.singlecontainer`](../docker/Dockerfile.singlecontainer):
a node stage builds the SPA, a .NET SDK stage publishes the Api, and the runtime stage adds ffmpeg,
the bundled fonts and the demo assets.

`docker-compose.yml` is unchanged and remains the honest three-container architecture — nginx, Api
and Worker sharing a volume. Single-container mode is a configuration of the same code, not a
second application. See [ARCHITECTURE.md](ARCHITECTURE.md).

Two details that break static-ish deploys and are handled explicitly:

- **Base URL.** The compose build sets `VITE_API_BASE_URL=/api` because nginx proxies under that
  prefix. The single-container build sets it to the **empty string**, because the API serves the
  bundle from its own origin and its routes are at `/jobs`. CI asserts the deployed bundle contains
  no `"/api"`.
- **Port.** Render injects `$PORT` at runtime. An exec-form `ENTRYPOINT` performs no variable
  expansion, so the entrypoint is `sh -c "exec dotnet … --urls http://+:${PORT:-8080}"`. The `exec`
  keeps dotnet as PID 1 so it still receives `SIGTERM` and shuts the hosted services down cleanly.

## Live

**https://dj-visualizer.onrender.com** — deployed 2026-08-21 from `render.yaml`, verified end to
end by `scripts/verify-deployment.sh` (9 checks, all passing).

Measured on the free instance, for calibration against the numbers elsewhere in these docs:

| | Free instance | Development machine |
|---|---|---|
| 24 s sample, 720p | **67 s** | 8 s |
| 24 s sample, 1080p | **94 s** | 9 s |
| Output size (720p) | 12.5 MB | 3.6 MB (`veryfast` rather than `ultrafast`) |
| Cold start after 15 min idle | up to ~60 s | n/a |

1080p renders comfortably within 512 MB — the memory headroom concern this document previously
raised turned out to be unfounded, and is recorded here as measured rather than assumed.

### What the progress bar does here

Worth knowing because it is the one place the free tier's slowness is user-visible. Measured on
the 1080p render above:

```
  0s    0%   render starts
  7s    5%   circular artwork still done
 10s   10%   ambient background still done
 24s   11%  ┐
 ...         │ rotation pass, ~34 updates from inside itself
 68s   45%  ┘
 70s   49%  ┐
 ...         │ mux
 94s  100%  ┘
```

The bar previously sat at **0% for 64 of those 94 seconds**, because only the mux pass reported
progress — which on a machine where the earlier passes take two seconds is invisible, and here read
as a hung page. See `RenderProgressScale`. The longest remaining flat stretch is the 14 seconds at
10% before the rotation pass emits its first line; the two single-frame passes have nothing
granular to report, which is the floor rather than something left undone.

`plan: free` was accepted for a Docker web service, which Render's own documentation does not state
either way — worth knowing, since the free-tier page lists only language runtimes.

## Deploying

Exactly one manual step exists, and it is the account. Everything before and after it is
automated: [`render.yaml`](../render.yaml) configures the service, and no `envVar` uses
`sync: false`, so the blueprint asks no questions when applied.

1. Sign in at [render.com](https://render.com) with **GitHub** — it is an OAuth authorisation, not
   a signup form, and no card is requested on the free path.
2. **New → Blueprint**, pick this repository, apply. Render reads `render.yaml`.
3. First build takes roughly 5–10 minutes (a node stage, a .NET publish stage, and the ffmpeg apt
   install).
4. The service comes up at `https://<name>.onrender.com`.

Then record the URL so the live checks start running:

```bash
gh variable set PRODUCTION_URL --body "https://<your-service>.onrender.com"
```

That is all the configuration there is. From then on `autoDeploy: true` redeploys on every push to
`main` with no further interaction, and
[`.github/workflows/verify-deployment.yml`](../.github/workflows/verify-deployment.yml) checks the
live site after each deploy and once a day.

To check it by hand at any point:

```bash
bash scripts/verify-deployment.sh https://<your-service>.onrender.com
```

That script wakes the instance, asserts the SPA document and both content-security policies, checks
that a deep link falls back to the SPA, reads `/limits`, then renders the bundled sample end to end
and downloads the MP4 — with a caption full of filtergraph metacharacters, so it re-proves
[SECURITY.md](SECURITY.md) F-1 against the deployed build rather than only in CI.

**On being charged.** Three things stand between this deployment and a bill, and they are worth
separating because only one of them is under this repository's control end to end.

*The plan cannot escalate.* `plan: free` is the only plan named in the blueprint, so it cannot
silently provision a billable instance — an invalid plan fails the blueprint with an explicit
error.

*The metered resource is capped below its allowance.* Compute on the free plan is fixed-price, so
an abuser spending CPU can only make the demo slow. **Bandwidth is the only thing an anonymous
visitor can spend that a host meters**, and downloads are the only large responses this app
serves. `Jobs__MaxEgressBytesPerWindow` holds that to 3 GB per 24 hours — about 90 GB in a 30-day
month, under Render's 100 GB free allowance — so overage is unreachable rather than merely
unlikely. Past the cap, downloads answer 503 until the window rolls and rendering keeps working.
Two narrower limits sit inside it: 5 downloads per job and 20 downloads per 5 minutes per IP.
[SECURITY.md](SECURITY.md) F-5 has the reasoning, including why the instance-wide one is the only
one of the three that bounds a total.

*And the platform backstops it.* With no payment method on file, Render suspends a free service
that exceeds its limits rather than billing for the overage. This is the least load-bearing of the
three — it depends on Render's current terms and on the account genuinely having no card, neither
of which this repository can assert. Treat it as the last line, not the plan.

Render can deploy a **private** repository, so the source does not have to be public for the demo
to work. For a portfolio it probably should be — that is a separate decision from deploying.

### Why there is no account-free option

Worth stating, because it is the first question anyone asks. This app needs a long-running process,
a ~100 MB native ffmpeg binary, writable disk, and minutes of CPU per job. That rules out every
free host that does not require an identity — GitHub Pages, Cloudflare Pages and Netlify are static
only and cannot execute .NET or ffmpeg at all. GitHub Actions can run the workload but is CI, not
hosting: it exposes no inbound HTTP endpoint. Codespaces can forward a port, but only while a
codespace someone started by hand is still running.

So the choice is not "Render versus something with no account" — it is "one OAuth versus no live
app." The nearest account-free alternative is a static showcase on GitHub Pages (a recorded demo
plus the docs), which needs no signup but does need the repository to be public, since
[Pages on the free plan does not serve private repos](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

### Environment variables

All set by `render.yaml`; none is a secret, because the app has no secrets.

| Variable | Demo value | Default | Why it differs |
|---|---|---|---|
| `Jobs__SingleContainer` | `true` (in the Dockerfile) | `false` | Host the worker in-process and serve the SPA |
| `Jobs__RootPath` | `/tmp/djvisualizer-jobs` | content root | Ephemeral disk |
| `Jobs__MaxAudioBytes` | 60 MB | 2 GB | 512 MB RAM, ephemeral disk |
| `Jobs__MaxImageBytes` | 10 MB | 25 MB | Same |
| `Jobs__MaxDurationSeconds` | 600 (10 min) | 21600 (6 h) | Same, plus a smaller output file per job |
| `Jobs__MinFreeDiskBytes` | 100 MB | 3 GB | The default would refuse every job on a small instance |
| `Jobs__MaxEgressBytesPerWindow` | 3 GB | 0 (unlimited) | The only limit here about the bill rather than the box — see "On being charged" |
| `Jobs__EgressWindowHours` | 24 | 24 | — |
| `Demo__Enabled` | `true` (in the Dockerfile) | `false` | Enables `POST /jobs/sample` |
| `Worker__X264Preset` | `ultrafast` | `veryfast` | ~0.1 CPU; only ~60 frames are ever encoded |
| `Worker__RetentionMinutes` | 30 | 60 | Small ephemeral disk |

A malformed value does **not** crash the container: `JobsOptionsFactory` logs a warning and uses
the documented default. `Jobs__SingleContainer` accepts `true/false/1/0/yes/no/on/off`, and an
unrecognised value falls back rather than being guessed at.

### Migrations and seeding

Neither exists. There is no database, and the demo assets ship inside the image rather than being
seeded into storage. See [DEMO_DATA.md](DEMO_DATA.md).

## How deploys trigger

`autoDeploy: true` — every push to `main` rebuilds and redeploys.

**CI and deploy run in parallel, not gated.** Render starts building on the push at the same time
GitHub Actions runs the tests, so a red build can reach production. That is an accepted trade-off
for a portfolio app: it keeps the free tier's build minutes and the feedback loop short, and the
blast radius of a bad deploy is a demo site. To gate it instead, set `autoDeploy: false` in
`render.yaml` and add a deploy step to CI that calls Render's deploy hook after the `docker` job
passes.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| First request hangs ~1 minute | Free instance spun down after 15 min idle | Expected. Wait; it is warm afterwards. |
| Every upload returns **503 "low on disk space"** | `Jobs__MinFreeDiskBytes` above what the instance has free | Lower it; the 3 GB default is for real hardware. |
| Job goes `Failed` right away with "audio could not be read" | ffmpeg missing, or the file is not really the format its extension claims | Check `ffmpeg -version` in the image; the signature validator rejects mismatched files by design. |
| Job sits **`Queued`** forever | Worker not running in-process | `Jobs__SingleContainer` is not `true`. Check the startup logs for a malformed-value warning. |
| Caption renders as an empty box or the render fails | Font path wrong for the image | `Worker__FontFilePath*` must point inside `/app/fonts/`, populated by the Dockerfile. |
| Page loads blank, console shows CSP errors | API-strength CSP served on the HTML document | `SecurityHeadersMiddleware` negotiates on content type; check the document's `Content-Type` really is `text/html`. |
| API calls 404 on the deployed site | Bundle built with the wrong base URL | It must be built with `VITE_API_BASE_URL=""`, not `/api`. CI asserts this. |
| Build fails on duplicate `appsettings.json` | Api referencing the Worker **executable** instead of `Worker.Hosting` | Reference the library. |
| OOM during a 1080p render | 512 MB is tight for the blurred background pass | Use 720p on the free tier, or raise the instance size. |

## Free-tier headroom

- **Instance hours:** 750/month against ~730 hours in a month; spin-down means real usage is well
  under. Comfortable.
- **Disk:** each job is the upload pair plus the output. At 60 MB in, a job's peak footprint is a
  few hundred MB, and retention is 30 minutes. One render at a time, so this is bounded.
- **CPU:** the real constraint. Rendering is single-job-at-a-time by design, and the queue absorbs
  concurrency rather than thrashing.
- **Bandwidth:** a rendered video is the large object. A 15-minute 720p render is on the order of
  100 MB, so sustained traffic would be the first limit reached in practice.
