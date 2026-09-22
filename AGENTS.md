# Spinner engineering decisions

Keep the existing .NET/React product scope. AI critique is a separate, unverified project.

- Run `npm run verify` at the root. It checks project boundaries, lint, format, typed builds and all tests. FFmpeg/ffprobe are required; skipped backend tests fail the gate.
- First install frontend packages with `npm ci --prefix frontend`; enable the hook with `npm run prepare`. `start-app.bat` is the Windows public launcher (5080); Vite development uses 5173 strictly.
- Job lifecycle and validation belong in Domain; orchestration and ports in Application; disk/process implementations in Infrastructure; API and Worker.Hosting compose them. `scripts/architecture.mjs` rejects reversed project references. Keep raw console logging out of application source; use the configured ILogger sink.
- Exactly one API and one worker may own a jobs directory. OS file leases enforce this across processes. Do not add replicas without replacing the queue and admission coordination.
- Download admission uses the singleton JobDownloadGate through read/check/persist. Never move the lock around the long file transfer. Preview does not mutate the five-save allowance; both routes reserve a whole file conservatively against process-local egress before transfer.
- Filesystem writes use unique temp files and rename. This is not a general transactional database: worker/cleanup ownership and single-instance limits still matter.
- Synthetic demo files are generated from scripts. Never copy jobs-data, personal mixes, credentials or downloaded artwork into public fixtures or the Docker context.
- The app has no user ownership: possession of a random job URL grants access until expiry. Do not describe this as authenticated privacy.
- Build both default and standalone frontend modes. The latter uses an empty API prefix; an unset value means /api and is different.
- Render must use After CI Checks Pass. Live verification requires an expected commit and decodes a real rendered MP4. Never call a deployment verified from health alone.
- Preserve uncommitted work. Describe actual test/deployment evidence, with remaining unchecked areas explicit.
