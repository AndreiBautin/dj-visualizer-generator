# Spinner engineering decisions

Keep the existing .NET/React product scope. AI critique is a separate, unverified project.

- Run `npm run verify` at the root. It checks project boundaries, lint, format, typed builds and all tests. FFmpeg/ffprobe are required; skipped backend tests fail the gate.
- First install frontend packages with `npm ci --prefix frontend`; enable the hook with `npm run prepare`. `start-app.bat` is the Windows public launcher (5080); Vite development uses 5173 strictly.
- Job lifecycle and validation belong in Domain; orchestration and ports in Application; disk/process implementations in Infrastructure; API and Worker.Hosting compose them. `scripts/architecture.mjs` rejects reversed project references. Keep raw console logging out of application source; use the configured ILogger sink.
- Exactly one API and one worker may own a jobs directory. OS file leases enforce this across processes. Do not add replicas without replacing the queue and admission coordination.
- Download admission uses the singleton JobDownloadGate through read/check/persist. Never move the lock around the long file transfer. Preview does not mutate the five-save allowance; both routes reserve against process-local egress before transfer - a download the whole file, a preview the bytes its single range will send (anything ASP.NET would answer in full is charged in full; see `PreviewRange`). Never charge a preview the whole file per request again: five one-byte ranges spent the 3 GB window.
- Filesystem writes use unique temp files and rename. This is not a general transactional database: worker/cleanup ownership and single-instance limits still matter.
- Synthetic demo files are generated from scripts. Never copy jobs-data, personal mixes, credentials or downloaded artwork into public fixtures or the Docker context.
- The app has no user ownership: possession of a random job URL grants access until expiry. Do not describe this as authenticated privacy.
- Build both default and standalone frontend modes. The latter uses an empty API prefix; an unset value means /api and is different.
- Render must use After CI Checks Pass. Live verification requires an expected commit and decodes a real rendered MP4. Never call a deployment verified from health alone.
- UI uses the tokens in `index.css`, `lib/surfaces.ts` and `lib/buttons.ts` (see docs/ARCHITECTURE.md, "The frontend's visual system"); no raw `white/NN` text colours or one-off button styles. Keep animation `motion-safe:`, and give decorative layers `overflow-hidden` so they cannot widen `main`'s scroll area.
- The video copies the page: `VinylFilterGraphBuilder` takes its proportions from `VinylRecord.tsx`, `lib/captionFit.ts` and `ExampleStrip.tsx` take theirs from it, and its glow constants are 14% of `--color-accent` (a gate test fails if they drift). Change one side, change the other.
- Visual baselines are per platform (`frontend/visual/__screenshots__/<platform>/`). Record Windows locally with `npm --prefix frontend run test:visual -- --update-snapshots`; Linux ones come from CI's `visual` artifact, not Docker. The job only warns, so review its artifact after UI changes.
- Browser tests: `E2E_BROWSERS=all` adds Firefox and WebKit (CI runs all three). Click visible options, never force-check the sr-only radios.
- The service worker's `navigateFallbackDenylist` must cover every API path under both prefixes (`/api/...` and bare `/jobs/...`); a missing one turns downloads into `index.html`.
- Caption fonts: Poppins and Space Mono ship Latin WOFF2 subsets from `scripts/subset-caption-fonts.sh`. Never subset Abril Fatface - its OFL reserves the name.
- Every read of an uploaded file goes through `FfmpegInputFormat` (forced demuxer, file protocol only). Without it ffmpeg sniffs contents, and a `.wav` holding a concat script was probed as concat.
- `Hosting__TrustForwardedFor` is for a proxy that appends the hop (Render) and nothing else; rate limits partition IPv6 by /64. Uploads must be deleted on every path that does not enqueue - cleanup cannot see a folder without `status.json` until the stale threshold.
- Actions and base images are pinned by SHA/digest; change them through Dependabot, not by editing tags. The entry bundle budget (110 KB gzipped) only goes down without a stated reason.
- On the free host only `ultrafast` has completed a 40-minute render; veryfast and superfast/1-thread each lost one to a restart. Measure live before changing the preset.
- Preserve uncommitted work. Describe actual test/deployment evidence, with remaining unchecked areas explicit.
