# Deployment

## Existing host and current status

The public URL is https://dj-visualizer.onrender.com. It was suspended (by its owner) as of the 2026-09-21 assessment; resumed and verified live since — `scripts/verify-deployment.sh` passed 9/9 against the exact deployed commit, including a real render, download and ffprobe/ffmpeg decode.

Keep the existing Render free web service; do not provision paid services or add a payment method. The single container serves React and hosts the same worker library used by Docker Compose. No database or persistent disk is required for this disposable demo. Static hosting cannot run FFmpeg; a multi-service deployment adds infrastructure without helping the single-worker demo.

Render's current documentation describes free instances and their restrictions at https://render.com/docs/free. Free instances can sleep and restart, and ephemeral files disappear. Provider quotas and account settings must be checked in the dashboard; process-local bandwidth reservations are not a monthly spending guarantee. No new provider/card-required migration is proposed.

## Build and configuration

`docker/Dockerfile.singlecontainer` builds the SPA, publishes the API, and includes generated demo assets and OFL fonts. `.dockerignore` excludes jobs-data, logs, credentials, local demo output and verification artifacts.

`render.yaml` selects `plan: free`, caps uploads/duration, sets retention and font paths, and enables sample rendering. `.env.example` documents configuration. The SPA uses an empty `VITE_API_BASE_URL` in standalone mode (unset means `/api`), and `VITE_SAMPLE_ENABLED=true`. VITE variables are public bundle content: never put credentials in them.

`Jobs__SingleContainer=true` hosts API and worker together. `Jobs__RootPath` must be shared by separate API/worker containers. OS leases permit only one API and one worker per root; do not scale replicas. Local launch uses port 5080; compose exposes UI 5173 and API 5080.

## Release gate

`npm run verify` runs the local gate; CI uses that same command, then dependency audits, full-history Gitleaks, Docker builds and browser tests. `render.yaml` sets `autoDeployTrigger: checksPass`, the documented [After CI Checks Pass](https://render.com/docs/deploys) setting. Confirm that setting in the existing dashboard: a YAML file alone does not prove it was applied to an existing service. A new commit with failed checks must not deploy.

Do not trigger live verification as a required check before deployment: that creates a circular dependency. The Verify deployment workflow is manual or daily; after a release, run it for the deployed branch. Set the GitHub repository variable `PRODUCTION_URL`. Missing URL fails explicitly. The script waits for `/version` to match the expected commit before rendering, then downloads and decodes the result.

```sh
bash scripts/verify-deployment.sh https://dj-visualizer.onrender.com "$(git rev-parse HEAD)"
```

The script requires curl, ffprobe and ffmpeg — no Python. It checks the page, health, headers, deep links, limits, sample submission, completion and actual MP4 decoding. Browser playback/seek and console checks complement this script.

## Operations and limits

There are no migrations. Demo fixtures are bundled, not imported from personal storage. Cleanup expires temporary jobs; never reset a real user's jobs directory for a test. Use a separate root for smoke runs.

**Every deploy drops jobs in flight.** Each push to `main` that passes CI redeploys, which replaces the instance and its ephemeral disk: a render in progress, and every finished file, disappears, and their links return 404 (the page says the render expired or the server restarted, and removes it from recent renders). This was observed on 2026-10-13, when a 40-minute render at 67% vanished on a redeploy. Do not push during a demo or while someone's long render is running; a persistent disk would be the fix, and is a paid feature on this host.

Downloads and previews are charged against a process-local egress budget that resets on restart; the charging rules are in [SECURITY.md, "F-5" and "Hardening batch"](SECURITY.md). Rate limits and caps reduce abuse rather than guarantee availability. Long files still cost disk and bandwidth; the 45-minute public ceiling has not been load-tested at its limit. **Current measurement (free instance, 2026-10-14, fa17ebf):** a 40-minute mono WAV (212 MB) uploaded in about 37 s and rendered at 1080p in **435 s**, producing a **719 MB** MP4 with the sample artwork. ultrafast's bitrate depends heavily on the artwork, and the loop's bytes are paid in every download, so **five saves of that (about 3.6 GB) exceed the 3 GB egress window - an open overrun**, bounded only by the window itself refusing further downloads. `Worker__X264Crf=28` (render.yaml) is the configured fix - same preset and memory, about 36% fewer bytes per loop locally, which would bring five saves of that render to roughly 2.4 GB - but it has not run live yet, because Render stopped deploying after `cf7f963`. Two attempts to shrink the file, veryfast and superfast on one thread, each lost a 40-minute render to an instance restart (at 17% and 38%); the cause was not established (no instance events were read) and both were reverted. *History:* before the mux stopped re-encoding AAC and the caption moved into the backdrop, the same kind of render took about 21.6 minutes and produced 382 MB with different artwork. Decision (2026-10-14): keep the 45-minute cap and five saves for long mixes; the bound is acceptable for a demo, and fewer saves, forced 720p or a shorter cap were each judged worse for visitors than the egress they would save.

| Symptom | Action |
| --- | --- |
| Service Suspended | Inspect account/service status; distinguish intentional suspension from sleep. |
| Health never becomes ready | Check free-disk threshold, process startup and logs. |
| Second instance fails startup | Stop the existing role; do not delete a lease marker to bypass ownership. |
| Sample unavailable | Check Demo settings and bundled asset paths. |
| Render fails locally | Confirm ffmpeg/ffprobe on PATH and all three configured font paths. |
| Wrong or null version | Configure BUILD_COMMIT or the host's RENDER_GIT_COMMIT; verify expected revision. |
| Preview or download returns 503 | The process budget may be exhausted; inspect limits instead of retrying aggressively. |
| Job disappears after deployment | Ephemeral storage was lost; create a new job. |
