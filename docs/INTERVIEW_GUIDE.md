# Interview guide

## Thirty seconds

“I built Spinner to turn a DJ mix and cover art into a video. The useful optimization is that the animation repeats: I encode one rotation and stream-copy it for the rest of the mix. Audio processing and output still scale with duration, but repeated video encoding is avoided. It is .NET and React with asynchronous jobs, real FFmpeg tests and a deliberately single-instance filesystem queue.”

## Three-minute demonstration

Start the bundled sample, explain settings and progress, play and seek the result, then save it. Show `ProcessRenderJobUseCase` and a real FFmpeg test. Explain the single-instance boundary. Verify the hosted site before a call; keep a local sample ready. The last assessed public status was suspended.

## Decisions to explain

- **Why layers?** Job transitions and upload constraints are real domain rules. Application orchestrates; Infrastructure handles files/processes. Project reference checks enforce direction. No CQRS or microservices are needed.
- **Why files?** Short-lived jobs, one worker, no relationships or query requirements. Writing queued status is the enqueue. The costs are directory scans, ephemeral storage and limited concurrency; OS leases prevent accidentally starting replicas.
- **Why separate preview?** Browsers make range requests during playback. Preview supports seeking without spending the five-save allowance, while remaining rate/budget limited.
- **What protects public compute?** Upload validation and limits, rate limits, free-disk checks, serial rendering, retention cleanup and process-local egress admission. These reduce abuse; they do not eliminate denial of service or guarantee provider billing limits.
- **What proves it?** One verification command, real media tests, API range/concurrency tests, browser tests, and an expected-commit live check that downloads and decodes output. Cite the latest recorded run, not a memorized test count.
- **How does privacy work?** No accounts. A random job URL is a bearer capability, not ownership authorization. Files expire; public fixtures are generated content.

## Honest tradeoffs

The filesystem is not a distributed queue. More workers require atomic claims and durable coordination. Egress budgets reset after restart and overcount partial/aborted responses. Long mixes produce large files even with cheap encoding. Free hosting may sleep or restart; enterprise availability is not implied.

This complements the résumé's .NET/React, asynchronous integration and production-stabilization experience. Demonstrate those habits in a smaller inspectable system. Do not claim the separate AI listening experiment works in Spinner, total render cost is constant, or a suspended URL is live.
