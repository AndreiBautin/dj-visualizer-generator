# Interview guide

## Thirty seconds

“I built Spinner to turn a DJ mix and cover art into a video. The useful optimization is that the animation repeats: I encode one rotation and stream-copy it for the rest of the mix. Audio processing and output still scale with duration, but repeated video encoding is avoided. It is .NET and React with asynchronous jobs, real FFmpeg tests and a deliberately single-instance filesystem queue.”

## Three-minute demonstration

Start the bundled sample, explain settings and progress, play and seek the result, then save it. Show `ProcessRenderJobUseCase` and a real FFmpeg test. Explain the single-instance boundary. It's live and was last verified end-to-end (real render, download, decode) on 2026-09-26 — open it before a call anyway, since a free instance sleeps after 15 minutes idle and the first request can take up to a minute; keep a local sample ready regardless.

## Decisions to explain

- **Why layers?** Job transitions and upload constraints are real domain rules. Application orchestrates; Infrastructure handles files/processes. Project reference checks enforce direction. No CQRS or microservices are needed.
- **Why files?** Short-lived jobs, one worker, no relationships or query requirements. Writing queued status is the enqueue. The costs are directory scans, ephemeral storage and limited concurrency; OS leases prevent accidentally starting replicas.
- **Why separate preview?** Browsers make range requests during playback. Preview supports seeking without spending the five-save allowance, while remaining rate/budget limited.
- **What protects public compute?** Upload validation and limits, rate limits, free-disk checks, serial rendering, retention cleanup and process-local egress admission. These reduce abuse; they do not eliminate denial of service or guarantee provider billing limits.
- **What proves it?** One verification command, real media tests, API range/concurrency tests, browser tests, and an expected-commit live check that downloads and decodes output. Cite the latest recorded run, not a memorized test count.
- **How does privacy work?** No accounts. A random job URL is a bearer capability, not ownership authorization. Files expire; public fixtures are generated content.

## UI decisions to explain

- **Why does the preview equal the render?** The record, caption and label crop on the page use the render's own ratios (`VinylFilterGraphBuilder`), so the form is a WYSIWYG editor, not a mock-up. That claim is checked, not asserted: real renders matched the caption-fit estimate within 0.6% of frame width, a split red/green image proved the crop lands where the editor shows it, and a gate test fails if the video's glow drifts from the page accent.
- **Why is the accent darker than the design started with?** axe measured white text on the original blue at 3.14:1, under the 4.5:1 AA minimum. Brand brightness lost to legibility; the brighter tone survives only in glows and gradients where no text sits. CI audits both colour schemes so the trade cannot quietly reverse.
- **What did cross-browser testing find?** Turning on Firefox exposed a real bug: the PWA service worker answered `/api` download navigations with `index.html` under the compose deployment. A live 200 MB upload exposed another: when the window was not producing frames, the view transition never ran its callback and the screen stayed on the form. Both were fixed with the cause written down - the animation now can never gate the state change.
- **What was deliberately not built?** Lazy-loading the example strip was tried and reverted: the caption fonts it would have deferred already load for the form's font picker, so it saved nothing and risked hiding content where IntersectionObserver never fires.

## Honest tradeoffs

The filesystem is not a distributed queue. More workers require atomic claims and durable coordination. Egress budgets reset after restart and overcount partial/aborted responses. Long mixes produce large files even with cheap encoding. Free hosting may sleep or restart; enterprise availability is not implied.

This complements the résumé's .NET/React, asynchronous integration and production-stabilization experience. Demonstrate those habits in a smaller inspectable system. Do not claim the separate AI listening experiment works in Spinner, or that total render cost is constant.
