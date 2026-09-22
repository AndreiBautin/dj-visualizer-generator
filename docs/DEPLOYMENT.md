# Deployment

## Existing host and current status

The configured public URL is https://dj-visualizer.onrender.com. The 2026-09-21 assessment observed an owner-suspended service. That does not establish why. Do not claim the public demo works until the exact deployed revision completes a real render.

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

Both video endpoints reserve the whole file per request. Preview does not spend the five-save count, but range requests can conservatively consume the process budget faster than actual transferred bytes. The budget resets on restart. Rate limits and caps reduce abuse rather than guarantee availability. Long files still cost disk and bandwidth; the 45-minute public ceiling has not been load-tested on hosted hardware in this pass.

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
