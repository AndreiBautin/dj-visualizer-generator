# Testing

## Numbers

| Suite | Before | After |
|---|---|---|
| Domain.Tests | 52 | 52 |
| Application.Tests | 29 | 31 |
| Infrastructure.Tests | 89 | 95 |
| Api.IntegrationTests | 17 | 55 |
| Worker.Tests | 10 | 10 |
| **Backend total** | **197** | **243** |
| Frontend (vitest) | 53 | 60 |
| **Total** | **250** | **303** |

All passing, zero skipped. ffmpeg is installed locally and in CI, so the `[RequiresFfmpegFact]` /
`[RequiresFfmpegTheory]` tests run for real rather than auto-skipping.

Playwright covers the browser happy path against the full docker-compose stack in CI.

## Strategy per layer

**Domain** — pure unit tests, no doubles. Value objects reject what they should
(`JobTitle` empty/overlong, `RotationSpeed` out of range, `VideoPreset` and `CaptionFont` unknown
names) and `Job` refuses illegal state transitions. This is where input validity is *defined*, so
it is where it is most densely tested.

**Application** — use cases against `NSubstitute` fakes of the interfaces they declare. Tests
assert orchestration: that a failed artwork save deletes the audio already written, that progress
is persisted as it is reported, that cancellation leaves a job `Processing` rather than marking it
failed.

**Infrastructure** — real filesystem in temp directories, real ffmpeg. `FileSystemJobStore`,
`FileSignatureValidator` and the ffmpeg argument/filter-graph builders are tested directly. The
builders are pure string functions specifically so the exact command line is assertable without
running anything.

**Api** — `WebApplicationFactory` over the real pipeline with only `IAudioProbe` stubbed (so a
16-byte fixture can stand in for an hour of audio). Status codes, `ProblemDetails` shapes, security
headers and rate limiting are exercised end to end.

**Frontend** — Testing Library against real user interactions. The API client is spied on; nothing
below it is mocked.

## What is prioritised, and why

Tests were added where the productionization created a property that must not silently regress.

**Trust boundaries.** `FileSignatureValidator` per format; an `.mp3` whose bytes are `MZ` is
rejected end to end; a path-traversal filename is neutralised; oversize uploads are cut off mid-
stream.

**Things that must not be destroyed.** `CleanupService` is tested from the "must not delete"
side — a job inside its retention window survives, a `Processing` job inside the stale threshold is
not failed.

**The security fixes.**
`RenderAsync_Handles_Titles_Containing_Filtergraph_Metacharacters` renders with **real ffmpeg**
for six hostile captions. This is the most important test in the suite, because the vulnerability
it covers ([SECURITY.md](SECURITY.md) F-1) shipped *underneath two passing unit tests* that
asserted escaping was applied without ever asking ffmpeg whether it was correct. The lesson is
encoded in the test's location: it lives with the renderer, not the string builder.

`ExecuteAsync_Does_Not_Leak_Renderer_Diagnostics_Into_The_Job_Error_Message` asserts the stored
message contains no diagnostic text, no server path, and not even the words `ffmpeg` or `ffprobe`.

**The demo-data guarantee.** `DemoAssetSafetyTests` scans both bundled fixtures for emails, URLs,
phone numbers, credential patterns, private-key blocks and home-directory paths, and checks
container metadata structurally — no PNG text chunks, and only ffmpeg's own `TSSE` frame in the
MP3's ID3v2 tag. If someone swaps in a real track, CI fails.

**Configuration cannot crash or lie.** `JobsOptionsFactoryTests` covers the spellings of true and
false people actually type, and asserts that an unrecognised value falls back **and warns** rather
than being guessed. A malformed byte count must not be read as zero — `UploadLimits` rejects a
non-positive limit, so that would only move the crash a few lines later. Every malformed value is
reported, not just the first.

**The deployed configuration specifically.** The sample endpoint is off by default, 404s when
disabled, 503s without disclosing server paths when its assets are missing, and honours render
settings. `/limits` reflects configuration rather than compiled-in defaults. The SPA states the
server's limits and falls back to the built-in ones when that request fails.

## What is deliberately not tested

- **Visual correctness of the video.** No test asserts the disc is round or the caption is
  centred. Pixel comparison against ffmpeg output across versions and platforms is brittle enough
  to become noise. It is verified by extracting a frame and *looking* at it, which was done during
  this work — including with a caption containing an apostrophe, colon and percent sign.
- **ffmpeg itself.** Tests assert the arguments and filter graphs produced, and that a real render
  succeeds and probes as the right codec, dimensions and duration. They do not test the encoder.
- **Render performance.** Timings in the docs are measured by hand and reported as measurements,
  not asserted. A timing assertion on shared CI hardware is a flaky test.
- **Concurrent workers.** The design assumes one worker instance. Testing a race that the
  architecture states it does not support would be testing fiction; the assumption is documented
  instead.
- **The three-container compose stack, in unit tests.** It is covered by the Playwright e2e job in
  CI, which is the only place it is a real system.
- **Kestrel, ASP.NET model binding, React, TanStack Query.** Framework behaviour.

## Running them

```bash
dotnet test backend/DjVisualizer.sln
```

```bash
npm --prefix frontend run test
```

```bash
npm --prefix frontend run test:e2e
```

The e2e run needs the stack up (`docker compose up --build -d`).

## Test helpers worth knowing

- **`RequiresFfmpegFactAttribute` / `RequiresFfmpegTheoryAttribute`** — auto-skip when ffmpeg is
  absent, so the suite stays green on a machine without it while running for real in CI. The theory
  variant was added for the filter-graph injection cases.
- **`JobsApiFactory`** — `WebApplicationFactory` with a temp jobs root that is deleted on dispose,
  a stubbed `IAudioProbe` with a settable duration, and init-only properties for the demo switch
  and asset paths, so the enabled, disabled and missing-assets cases are each a separate factory.
- **`SilentWavBuilder`** — generates a valid WAV of a given duration in memory, so audio tests need
  no binary fixtures in the repository.
- **`StubAudioProbe`** — returns a caller-supplied duration, which is what lets a 16-byte fixture
  stand in for a six-hour set.
