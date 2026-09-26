# Spinner: senior developer portfolio demo readiness

## Current result — 2026-09-26

The existing product is ready for a portfolio walkthrough. The [public demo](https://dj-visualizer.onrender.com/) and [public source](https://github.com/AndreiBautin/dj-visualizer-generator) are available. AI listening and critique remain outside this application scope.

Verified application revision: `ba87b36a3174ed9530f452b715e06de063ccae69`.

- Root `npm run verify` passed: architecture boundaries, lint, formatting, typed Release builds, **301 backend tests with zero skips**, **91 frontend tests**, the audit-gate regression test, and default/standalone frontend builds.
- [CI for this revision](https://github.com/AndreiBautin/dj-visualizer-generator/actions/runs/35809412680) succeeded. [Expected-revision deployment verification](https://github.com/AndreiBautin/dj-visualizer-generator/actions/runs/36240998193) succeeded.
- A fresh public browser session on September 26 submitted the bundled sample, observed render progress and completion, played the result, sought back using the native timeline, and downloaded the MP4. The video reached readyState 4 with advancing playback time; the browser console recorded no errors.
- The downloaded sample was independently probed and completely decoded with FFmpeg: **24.000 seconds, 1920×1080 H.264 video, AAC audio, 19,741,611 bytes**, no decode errors.
- The local pre-push hook is enabled and its executable bit is tracked. The architecture gate rejected a deliberately reversed Domain-to-Infrastructure dependency; the original project file was restored afterward.

### Gaps closed

Preview now supports ranges separately from counted saves. Concurrent save admission is serialized, file writes use unique temporary files, and process leases enforce the single API/worker boundary. The root gate requires media tools and rejects skipped backend tests. CI uses the same gate, deployment waits for successful checks, and the live verifier requires the expected commit and decodes rendered media. Startup, architecture, testing, security and interview documentation describe the implemented system.

### Deliberate limits

The free host sleeps, so open the demo before an interview and keep a downloaded sample available. Storage and coordination remain single-instance and ephemeral. The egress budget is process-local and resets on restart; range requests conservatively reserve the entire file. Job URLs are bearer capabilities, not authenticated ownership. Production load at the maximum upload/duration limits and physical-device accessibility were not established by this verification. Exhausted download links can still show an API error response; that edge case is documented rather than represented as polished UI. Incident ingest is intentionally disabled on the public demo.

The assessment below is preserved as a **historical September 21 baseline**, not the current deployment status or remaining task list.

---

## Historical assessment — 2026-09-21

Assessment date: 2026-09-21. Source inspected: commit `55727a6`.

## Verdict and scope

Spinner is a credible senior .NET/React portfolio project, but its public demo is not ready to share today. The deployed URL displays **Service Suspended**, explicitly stating that its owner suspended it. That observation does not establish why it was suspended.

This is an assessment of existing functionality, not authorization to implement or deploy changes. The AI listening/critique project is tabled. No new AI, reactive visuals, accounts, database, themes, or batch features are required to make this application convincing.

The existing product converts audio and artwork into a rotating-record MP4, supports render settings, shows job progress and a completed-video player, and provides a bundled synthetic sample. Those are enough for a portfolio demo.

## Why it fits the résumé

The résumé establishes senior experience in .NET, React, asynchronous processing, production stabilization, and Azure observability. Spinner can demonstrate those engineering habits through a smaller system the reviewer can inspect end to end. It should complement that experience rather than pretend to reproduce an enterprise platform.

Lead with three decisions:

1. Encode one rotation and repeat the encoded clip, avoiding repeated frame encoding. Explain that muxing, audio processing, output size, and transfer still scale with duration.
2. Model asynchronous jobs explicitly and use filesystem persistence for a deliberately single-instance application. Explain recovery, concurrency boundaries, and where this design stops being appropriate.
3. Treat anonymous media processing as bounded work: validate uploads, limit resources, expose useful progress and failures, and clean up temporary files.

The separate API/worker hosting options, domain validation, real FFmpeg integration tests, and deployment smoke checks provide substance behind those points. Architecture terminology and test totals alone are not the story.

## Prioritized work

| Priority | Work | Evidence and acceptance criterion |
| --- | --- | --- |
| P0: before sharing | Restore an available demo and prove the deployed revision | The public URL currently shows Service Suspended. After an authorized deployment, verify the expected commit, health, page load, sample submission, visible progress, completed playback, and downloaded MP4. Keep a short recorded walkthrough as a fallback. |
| P0: before sharing | Resolve the preview/download contract | `frontend/src/components/DownloadPanel.tsx` uses the same URL for video playback and downloading. `GetJobDownloadUseCase` charges each request against five downloads and reserves the entire file size; `JobsController` returns a file without explicitly enabling range processing. Establish intentional preview/range/budget semantics. In a browser, prove play, seek, reload, and then download work without unexpected exhaustion or a raw JSON error page. Exact browser behavior remains to be reproduced. |
| P0: before sharing | Make one reliable verification gate | There is no unified `verify` command. Frontend scripts cover lint, tests, and a typed build, but no format check. Compose backend and frontend checks, formatting, and required media tests into the documented gate; use it in CI and a pre-push hook. The release gate should fail when required media tests silently skip. |
| P0: before sharing | Gate deployment on that verification result | `render.yaml` enables automatic deployment, while deployment verification is a separate workflow that waits a fixed time. The latter skips when `PRODUCTION_URL` is absent and does not assert the deployed commit. Require successful verification before deployment and check the exact revision afterward; a previous healthy build is not evidence for a new release. |
| P1: reliability | Protect filesystem job mutations under concurrency | `FileSystemJobStore` uses a fixed temporary filename and read-modify-write updates. Download requests can concurrently update the same job; dequeue is not an atomic multiworker claim. Test simultaneous downloads for lost counts and write collisions, then serialize/atomically update where needed. Explicitly enforce/document one worker. A broker or database is not necessary just for this demo. |
| P1: credibility | Correct stale and excessive claims | README, TESTING, INTERVIEW_GUIDE, and BACKLOG disagree with current tests/features or runtime status. Remove the claim that a six-hour set costs about the same as five minutes without qualifying encoding versus total work. Record reproducible measurements, machine, settings, and output size. Reconcile completed UI work with the backlog. |
| P1: safeguards | Validate the security gates themselves | The NuGet audit step matches pipe-delimited severity text; prove that a representative high/critical finding actually fails it or parse structured output. This is a gate concern, not evidence that a vulnerable dependency was found. The in-memory egress budget resets on restart: document it as a guardrail, not a guaranteed monthly spending ceiling. |
| P1: maintainability | Finish the repository standards | Add project-specific AGENTS guidance, enforce intended dependency boundaries, and provide the documented `start-app.bat` entry point. Existing `run.bat` and standalone mode already do useful startup work; consolidate their public contract instead of creating another competing path. |
| P2: after the core path is proven | Audit small-screen and failure-state polish | Verify keyboard flow, reduced motion, narrow-screen layout, rejected uploads, render failure, expired links, and exhausted limits. The current UI already includes a completion player and recent polish; inspect those features before proposing replacements. Waveform decoding reads only a prefix of the file, so do not imply it represents the entire mix or that encoded byte limits fully bound decoded memory. |

## A reviewer-friendly demo

The default experience should use the already bundled synthesized audio and generated artwork. Personal mixes and downloaded anime artwork should not become public fixtures without establishing their publication rights.

For a three-minute walkthrough:

1. Explain the user problem in one sentence: turn a mix and cover art into a video suitable for a video platform.
2. Start the existing sample render with visible settings and progress. While it runs, explain the one-rotation optimization.
3. Play the finished video, seek, and download it. Have a previously generated sample available if infrastructure is unavailable.
4. Show the job lifecycle and one meaningful integration test. Explain a failure/recovery tradeoff and the single-instance boundary.
5. Close with a measured performance result and one honest limit, rather than a speculative feature roadmap.

Keep the landing README brief: what it does, a working demo, local startup, one screenshot/output example, verification, and the architectural insight. Link the deeper design and interview notes from there.

## Verification performed

- Current working tree was clean at inspection; latest commit was `55727a6` (frontend polish).
- Frontend lint completed without reported findings; all 90 tests passed; TypeScript/Vite production build passed.
- Backend Release test run: domain 58, application 44, worker 10, API integration 65 passed. Infrastructure initially passed 104 with six test cases skipped by its FFmpeg availability check.
- Focused infrastructure rerun with FFmpeg available: **115 passed, zero skipped**, including real media tests. Across the verified suites: **292 backend + 90 frontend = 382 passing tests**. The initial skip behavior still merits attention in the release gate; the exact cause of that first availability failure was not established.
- Opened the public demo in a browser and observed the suspension page.
- Inspected current UI, persistence/download handling, CI and deployment workflows, deployment settings, and documentation.

Not verified in this assessment: current remote CI status, Docker builds, Playwright end-to-end execution, current application UI on a local server, browser playback/seek behavior, simultaneous-download behavior, a deployed media render, accessibility/mobile behavior, load limits on hosted hardware, or dependency vulnerability results. Passing unit/integration tests does not establish those results. No complete `verify` gate exists to run yet.

## Recommended scope for the next implementation round

Implement the P0 items and the concurrency/documentation corrections needed to support their claims. Run the complete gate and prove the sample journey against the actual deployed revision. Keep the existing application scope. Assess the remaining P1/P2 items against observed failures rather than treating extra features as a condition of senior-level quality.
