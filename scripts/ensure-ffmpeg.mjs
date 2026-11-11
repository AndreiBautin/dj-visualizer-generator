// Makes ffmpeg and ffprobe usable on a CI runner without letting a package mirror hang the job.
//
//   node scripts/ensure-ffmpeg.mjs [extra-apt-package ...]
//
// A runner that already has working tools (and the extra packages) is used as it is. Otherwise
// apt installs them with bounded network timeouts and a bounded number of attempts, and the tools
// are checked again afterwards: success means the binaries run and can decode what the live check
// downloads, never just that apt exited 0. On 2026-11-11 an unbounded `apt-get update && install`
// spent 1h18m retrying after a DNS failure for azure.archive.ubuntu.com, and the real media check
// behind it never ran.
import { spawnSync } from 'node:child_process'
import { pathToFileURL } from 'node:url'

// Decoders the live check needs for the rendered MP4 (H.264 video, AAC audio).
const DECODERS = ['h264', 'aac']

export const APT_OPTIONS = [
  '-o',
  'Acquire::Retries=2',
  '-o',
  'Acquire::http::Timeout=20',
  '-o',
  'Acquire::https::Timeout=20',
  '-o',
  'DPkg::Lock::Timeout=60',
]

const PROBE_TIMEOUT_MS = 20_000
const UPDATE_TIMEOUT_MS = 2 * 60_000
const INSTALL_TIMEOUT_MS = 5 * 60_000

const succeeded = (result) =>
  !result.error && result.status === 0 && !result.timedOut

/** Returns the problems that make the current tools unusable; empty means usable. */
export function checkMediaTools(run, extraPackages = []) {
  const problems = []
  for (const tool of ['ffmpeg', 'ffprobe']) {
    const result = run(tool, ['-hide_banner', '-version'], PROBE_TIMEOUT_MS)
    if (!succeeded(result)) {
      problems.push(`${tool} is missing or does not run`)
    } else if (!result.stdout.startsWith(`${tool} version`)) {
      problems.push(`${tool} -version did not identify ${tool}`)
    }
  }
  if (problems.length === 0) {
    const result = run(
      'ffmpeg',
      ['-hide_banner', '-decoders'],
      PROBE_TIMEOUT_MS,
    )
    const listed = succeeded(result) ? result.stdout : ''
    for (const decoder of DECODERS) {
      if (!new RegExp(`^ \\S+ ${decoder}\\s`, 'm').test(listed)) {
        problems.push(`ffmpeg cannot decode ${decoder}`)
      }
    }
  }
  for (const pkg of extraPackages) {
    const result = run(
      'dpkg-query',
      ['-W', '-f=${Status}', pkg],
      PROBE_TIMEOUT_MS,
    )
    if (!succeeded(result) || result.stdout.trim() !== 'install ok installed') {
      problems.push(`package ${pkg} is not installed`)
    }
  }
  return problems
}

/**
 * Reuses valid preinstalled tools, otherwise installs them with apt in at most `attempts` bounded
 * tries. Throws when the tools are still unusable, so the job fails quickly and says why.
 */
export function ensureMediaTools({
  run,
  sleep,
  log,
  extraPackages = [],
  attempts = 2,
  backoffMs = 15_000,
}) {
  const before = checkMediaTools(run, extraPackages)
  if (before.length === 0) {
    log('ffmpeg and ffprobe are already installed and working; skipping apt.')
    return 'preinstalled'
  }
  log(`Installing because: ${before.join('; ')}`)
  const packages = ['ffmpeg', ...extraPackages]
  for (let attempt = 1; attempt <= attempts; attempt++) {
    // --error-on=any: an index that failed to download (e.g. DNS) fails the update instead of
    // being a warning, so install never runs against a broken mirror.
    const update = run(
      'sudo',
      ['apt-get', ...APT_OPTIONS, '--error-on=any', '-q', 'update'],
      UPDATE_TIMEOUT_MS,
    )
    const install =
      succeeded(update) &&
      run(
        'sudo',
        ['apt-get', ...APT_OPTIONS, '-q', '-y', 'install', ...packages],
        INSTALL_TIMEOUT_MS,
      )
    if (install && succeeded(install)) {
      const after = checkMediaTools(run, extraPackages)
      if (after.length === 0) {
        log(`Installed ${packages.join(', ')} on attempt ${attempt}.`)
        return 'installed'
      }
      throw new Error(
        `apt succeeded but the tools are unusable: ${after.join('; ')}`,
      )
    }
    const failed = install || update
    const why = failed.timedOut
      ? 'timed out'
      : `exited ${failed.status ?? failed.error?.message}`
    log(
      `apt-get ${install ? 'install' : 'update'} ${why} (attempt ${attempt}/${attempts}).`,
    )
    if (attempt < attempts) sleep(backoffMs * attempt)
  }
  throw new Error(
    `Could not install ${packages.join(', ')} after ${attempts} attempts.`,
  )
}

function runCommand(command, args, timeoutMs) {
  const result = spawnSync(command, args, {
    encoding: 'utf8',
    timeout: timeoutMs,
    killSignal: 'SIGTERM',
    env: { ...process.env, DEBIAN_FRONTEND: 'noninteractive' },
    // apt's progress goes straight to the log; probes are captured to be inspected.
    stdio: command === 'sudo' ? 'inherit' : 'pipe',
  })
  return {
    status: result.status,
    stdout: result.stdout ?? '',
    error: result.error,
    timedOut: result.error?.code === 'ETIMEDOUT',
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try {
    ensureMediaTools({
      run: runCommand,
      sleep: (ms) =>
        Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms),
      log: (message) => console.log(message),
      extraPackages: process.argv.slice(2),
    })
  } catch (error) {
    console.log(`::error::${error.message}`)
    process.exit(1)
  }
}
