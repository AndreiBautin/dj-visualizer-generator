# Security

## Threat model

This shapes everything below, so it comes first.

The app has **no accounts, no sessions, no cookies, and no database**. A visitor uploads two
files, gets a GUID, polls it, and downloads an MP4. Jobs are deleted on a retention timer.

What that removes is most of the usual list, and it removes it *structurally* rather than by
mitigation:

| Not applicable | Why |
|---|---|
| SQL injection | No database, no SQL, no ORM |
| CSRF | No cookies and no ambient authority — a forged request can only create a job the attacker already could |
| Session fixation / hijacking | No sessions |
| Privilege escalation, IDOR across users | No users, no roles, no ownership |
| Password handling, credential stuffing | No credentials anywhere in the system |
| XSS via stored user content | Captions are drawn into a video by ffmpeg, never rendered as HTML |

What is genuinely exposed:

1. **Two file-upload trust boundaries** feeding a native binary (ffmpeg) that parses hostile input.
2. **A caption string** that reaches an ffmpeg filter graph.
3. **A job id** in a URL path used to build filesystem paths.
4. **Two unauthenticated, resource-expensive endpoints** — rendering is CPU and disk work anyone
   can trigger, and downloading is bandwidth anyone can spend. On metered hosting the second is
   the one that costs money, which is a distinction this document originally missed (see F-5).
5. **Error text** travelling back out to callers.

Everything below concerns those five.

## Findings

### F-1 — drawtext filter-graph injection via the job title (High) — **fixed**

**What it was.** The caption was interpolated into the ffmpeg `filter_complex` string inside a
single-quoted `drawtext` option, escaped by hand:

```
drawtext=fontfile='…':text='<escaped title>':fontcolor=white:…
```

The escaping doubled backslashes and prefixed `:`, `'` and `%`. It looked right, and two unit
tests asserted that it happened. **It was wrong.** ffmpeg does not honour `\'` inside a
single-quoted option value: the quote terminates the option regardless. A title containing an
apostrophe therefore ended its own option early and injected the remainder into the filter graph.

**How it was found.** By rendering with hostile titles for real instead of asserting on the graph
string. Three of six cases failed immediately:

```
Error parsing filterchain '[with_vinyl]drawtext=fontfile='…':text='\'\:drawtext=text=pwned\:…
```

The two existing unit tests had passed the whole time, because they checked that escaping was
*applied*, not that ffmpeg *accepted* it. That is the lesson worth keeping from this finding.

**Impact.** ffmpeg's filter language can read files — the `movie` filter takes a path — so
injection into a graph is not merely a broken render. Beyond that it was a plain availability and
usability bug: every title containing an apostrophe failed, and DJ set titles contain apostrophes
constantly.

**The fix.** Not better escaping — *no escaping*. The caption is written to a temporary UTF-8 file
and passed as `textfile='<path>':expansion=none`. Caller-controlled bytes never enter the filter
graph string, so there is no escaping rule left to get wrong. `expansion=none` additionally
disables drawtext's `%{…}` expansion, so a title is drawn exactly as typed. The path escaping that
remains is applied only to application-generated paths (a temp filename, a configured font).

Pinned by `FfmpegVideoRendererTests.RenderAsync_Handles_Titles_Containing_Filtergraph_Metacharacters`,
which renders with real ffmpeg for six hostile titles including `':drawtext=text=pwned:x=0:y=0:'`
and `[0:v]split[a][b];[a]nullsink[c]`.

### F-2 — renderer diagnostics disclosed to callers (Medium) — **fixed**

`RenderException` and `AudioProbeException` carried ffmpeg's stderr, which includes the full
command line: absolute server paths, the font path, and the entire internal filter graph. That
message was stored on the job and returned verbatim by `GET /jobs/{id}` as `errorMessage`, then
displayed in the UI.

Fixed in `ProcessRenderJobUseCase`: the exception is logged with the job id, and the job records
one of two fixed, user-facing messages. Pinned by
`ExecuteAsync_Does_Not_Leak_Renderer_Diagnostics_Into_The_Job_Error_Message`, which asserts the
stored message contains none of the diagnostic text, no `/data/jobs` or `C:\Users` path, and not
even the words `ffmpeg` or `ffprobe`.

### F-3 — no Content-Security-Policy on SPA responses (Low) — **fixed**

`SecurityHeadersMiddleware` sent `default-src 'none'` on every response. Correct for a JSON API,
and harmless while nginx served the SPA separately — but once the API also serves the SPA
(single-container hosting) that policy blocks the page's own scripts and stylesheets, shipping a
blank page.

The middleware now negotiates on content type: `text/html` gets a real SPA policy, everything else
keeps `default-src 'none'`. Two relaxations in the SPA policy are load-bearing rather than
habitual, and are commented as such: `style-src 'unsafe-inline'` because the progress bar sets its
width via a React inline style, and `img-src blob:` because artwork is previewed before upload via
`URL.createObjectURL`. Scripts get no relaxation.

### F-4 — job data written into the source tree (Low) — **fixed**

`.gitignore` ignored `/jobs-data/` anchored at the repository root, but the Api and Worker each
fall back to a `jobs-data` directory beside their own content root when `Jobs__RootPath` is unset.
Uploaded audio and artwork could therefore land at `backend/src/Api/jobs-data/` — outside the
ignore rule and one `git add -A` away from being committed. Caught during this work with 19 such
directories present (all 16-byte test fixtures, no real audio).

The ignore rule is now unanchored (`jobs-data/`), so it matches wherever the fallback lands.

### F-5 — unmetered download endpoint: unbounded egress from one render (High) — **fixed**

**What it was.** `POST /jobs` and `POST /jobs/sample` carried `[EnableRateLimiting("job-creation")]`.
`GET /jobs/{id}/download` carried nothing at all, and neither did `GET /jobs/{id}`.

That is an amplification, not merely a missing limit. Creating a job is the expensive half for the
*server* — minutes of CPU on a tenth of a core — and it was the only half that was limited.
Downloading is the expensive half for the *bill*, and it was free: the finished MP4 sits on disk
for the whole retention window, a job id is a bearer token with no owner, and re-serving the file
costs no CPU. One permitted job creation therefore bought unlimited bytes.

**Why it mattered here specifically.** The threat model above named "anyone can spend the server's
CPU" as the residual risk and judged the blast radius acceptable, which it is — a free instance is
a fixed-price box, so CPU abuse degrades the demo and cannot cost anything. That reasoning is
correct and it was applied to the wrong resource. **Bandwidth is the only metered thing an
anonymous visitor can spend on this deployment**, and it was the one thing with no limit on it.

Concretely, at the demo's then-current 15-minute cap and the measured ~0.5 MB of video per second
of audio, one 1080p render produced a file approaching a gigabyte, downloadable without bound for
30 minutes. A `curl` loop against a single job id could have moved a month's included bandwidth in
an afternoon.

**The fix.** Three limits at three different scopes, because no single one of them bounds the
total:

| Limit | Scope | Where |
|---|---|---|
| 5 downloads per job | one job id | `Job.MaxDownloads` — a domain rule, persisted in `status.json` |
| 20 downloads / 5 min | one IP | `"job-download"` rate limit policy, `Api/Program.cs` |
| `MaxEgressBytesPerWindow` | the whole instance | `IEgressBudget` / `RollingWindowEgressBudget` |

The first two both scale with the number of jobs and callers, so neither caps a total; the third
does, and it is the one the hosting bill is actually bounded by. It is set to 3 GB per 24 hours in
`render.yaml` — roughly 90 GB per 30-day month, under the free plan's 100 GB allowance — and
defaults to **unlimited** for self-hosting, where bandwidth is not metered and a cap would only
break a legitimate download. Past the budget, downloads answer 503 and rendering is unaffected.

`GET /jobs/{id}` also picked up a rate limit (240/min per IP). That one is a hammering guard, not
a cost control: status responses are a few hundred bytes. It is sized against the real client —
the SPA polls every 2 seconds, so a visitor watching one render spends about 45 requests a minute.

Two details worth keeping:

- **`RecordDownload` deliberately does not touch `UpdatedAt`.** Retention sweeps key on that
  timestamp, so bumping it on download would have let anyone holding the id keep a job — and its
  video — alive indefinitely by re-downloading inside the retention window. Pinned by
  `JobTests.RecordDownload_Does_Not_Extend_The_Jobs_Retention_Window`.
- **The budget is reserved before the file is served, not measured after.** A response bigger than
  what remains is refused rather than discovered to have overshot. The reservation is also charged
  only once the job is known to be downloadable, so 404s and not-ready polls cost nothing — a
  guard that could be exhausted by requests transferring no bytes would be the denial of service
  it exists to prevent.

The demo's `MaxDurationSeconds` also came down from 900 to 600, which shrinks the unit rather than
the total. It is a smaller lever than the budget and is not what makes the guarantee.

It went back up, later, to 2700 (45 minutes) — 600 rejected the real DJ mixes the app exists to
render, which made the demo pointless for its actual purpose. The guarantee above still holds
unchanged: the total is bounded by `MaxEgressBytesPerWindow`, not by the per-job duration, so a
larger unit means fewer full-length downloads fit in the daily budget before 503s start, not a
larger total spend. See `render.yaml`'s own comment for the resulting per-download share of the
budget.

### F-6 — `AllowedHosts: "*"` (Low) — **accepted**

The API does not restrict the `Host` header. It generates no absolute URLs from it, sets no
cookies, and issues no password-reset links, so the usual host-header attacks have nothing to act
on. Behind Render this is additionally moot — the platform routes by hostname before the request
arrives. Documented rather than "fixed" with a setting that would change nothing.

## Controls that were already in place

These predate this work and are worth naming, because they are the reason the finding list is
short:

- **Upload validation at the boundary.** Extension allowlist *and* magic-byte signature check
  (`FileSignatureValidator`) — an `.mp3` whose first bytes are `MZ` is rejected. Covered by an
  integration test.
- **Size limits enforced while streaming.** `FileSystemJobFileStorage` counts bytes as it writes
  and aborts past the limit, deleting the partial file. `Content-Length` is never trusted.
- **No command injection surface.** ffmpeg is invoked with an argument **list**
  (`ProcessStartInfo.ArgumentList`), never a shell string. There is no shell in the path at all.
- **Path traversal closed twice.** `JobId.Parse` accepts only a strict `D`-format GUID, and
  `JobPaths.GetJobDirectory` independently re-checks that the resolved path stays under the jobs
  root. Uploaded filenames are discarded entirely — files are stored as `audio.<ext>` and
  `artwork.<ext>`. Covered by
  `Post_Jobs_Ignores_A_Path_Traversal_Attempt_In_The_Uploaded_File_Name`.
- **Rate limiting.** 10 job creations per minute per IP, which also covers `POST /jobs/sample`.
- **Disk exhaustion guard.** `IDiskSpaceChecker` refuses new jobs below `MinFreeDiskBytes` with a
  503, and `/health` reports Degraded.
- **Bounded retention.** `CleanupService` deletes terminal jobs after `RetentionMinutes` and fails
  jobs stuck in `Processing`, so storage cannot grow without bound.

## Secrets

There are none. The application has no API keys, no connection strings and no credentials of any
kind — a direct consequence of having no database, no auth and no third-party services.

The full git history was scanned during this work (`git grep` over `git rev-list --all` for key,
token, secret and private-key patterns): every hit was the word `CancellationToken`. CI now runs
**gitleaks over the full history** (`fetch-depth: 0`) on every push, so a future accidental commit
fails the build rather than sitting unnoticed.

`.env` is gitignored; `.env.example` is committed and documents every variable, including which
`VITE_`-prefixed values are compiled into the public bundle and must therefore never hold a
credential.

## Logging

Logs contain event names, job ids, statuses and durations. **They never contain user content** —
not the caption, not filenames, not file contents. This was already true before this work and it
is what makes it safe to leave logging on at `Information` in production. The one place where
sensitive-ish detail is deliberately logged is the renderer exception from F-2, which goes to the
log precisely so it does not go to the caller.

## Dependency scanning

CI fails on **high or critical** vulnerabilities, for both ecosystems:

- `dotnet list package --vulnerable --include-transitive`, with the result grepped for
  High/Critical (the command itself exits 0 regardless, so the gate has to read the output).
- `npm audit --audit-level=high`.

Gated at high rather than low deliberately: low-severity noise in build-time transitive packages
teaches people to ignore the step, which is worse than not having it.

This gate found a real issue the first time it ran — `nanoid < 3.3.18`, high severity. It was
fixed (`npm audit fix`, a three-line lockfile change) rather than the threshold being lowered.

Restores are locked: `dotnet restore --locked-mode` against committed `packages.lock.json` files,
and `npm ci` against `package-lock.json`. Dependency drift fails the build instead of silently
resolving something that was never tested.

## Remaining risks, stated plainly

1. **Anyone can spend the server's CPU.** There is no auth, so the rate limiter and the upload
   limits are the only things standing between the public demo and someone using it as a free
   transcoder. On the free tier the blast radius is one small instance that spins down anyway, and
   the demo's limits (120 MB, 45 minutes) are set with this in mind. On a self-hosted instance with
   the 2 GB defaults, **do not expose it to the internet without putting auth in front of it.**

   Note the distinction this list previously blurred: on the deployed demo, spending CPU cannot
   spend *money*. The instance is fixed-price, so the worst an abuser achieves is a slow demo.
   Bandwidth is the metered resource, and it is bounded by F-5's three limits rather than by
   anything about the render pipeline.

2. **Nothing here defeats a distributed attacker.** The per-IP rate limits partition on a single
   address, so enough distinct sources dilute them. That is why the egress budget exists and why
   it is instance-wide: it is the only limit whose guarantee does not depend on how many callers
   there are. It is also in-memory, so a restart forgives the spend so far — a deliberate trade
   (see `RollingWindowEgressBudget`), and the reason the platform's own suspension-on-overrun
   remains a backstop worth having rather than a redundancy.

3. **The rate limiter partitions on `RemoteIpAddress`.** Behind a proxy that is the proxy's
   address unless forwarded headers are configured, so it degrades toward a global limit. On the
   single-container deployment the app is the origin, so this is accurate there.

4. **A job id is a bearer token.** Anyone with the GUID can read a job's status and download its
   video. GUIDs are unguessable and jobs are deleted within the retention window, but a leaked URL
   is a leaked video. Adding accounts would fix this and was deliberately not done.

5. **ffmpeg parses untrusted media.** A malicious file targeting an ffmpeg parser vulnerability is
   the most plausible remote-code-execution path in this system. Signature checks reduce the file
   types that reach it; they do not make ffmpeg safe. The realistic mitigations are keeping the
   base image current (the Dockerfiles install ffmpeg from the distro, so a rebuild picks up
   patches) and the fact that the container holds nothing worth stealing. Not mitigated:
   sandboxing ffmpeg further, e.g. seccomp or a separate unprivileged container per render.

6. **The container runs as root.** The .NET base images default to it and this was not changed —
   noted rather than quietly ignored.

7. **The `Demo__Enabled` flag and the frontend's `VITE_SAMPLE_ENABLED` can disagree.** They are
   set together per deployment. A mismatch degrades gracefully — the button appears and the server
   answers 404, which the UI surfaces as an error — but it is two switches where one would be
   better.
