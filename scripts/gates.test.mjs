import { test } from 'node:test'
import { throws, doesNotThrow } from 'node:assert/strict'
import { audit } from './audit-nuget.mjs'
test('audit rejects high and critical findings, including transitive packages', () => {
  for (const severity of ['High', 'Critical']) {
    throws(() =>
      audit({
        projects: [
          {
            frameworks: [
              {
                transitivePackages: [
                  { id: 'unsafe', vulnerabilities: [{ severity }] },
                ],
              },
            ],
          },
        ],
      }),
    )
  }
  doesNotThrow(() => audit({ projects: [] }))
  throws(() => audit({ logs: [{ level: 'error' }], projects: [] }))
  throws(() => audit({}))
})

test("the rendered video's backdrop glow is 14% of the page accent", async () => {
  const { readFileSync } = await import('node:fs')
  const { readAccentSrgb } = await import('./accent.mjs')
  const { strictEqual } = await import('node:assert/strict')
  const [, green, blue] = readAccentSrgb()
  const builder = readFileSync(
    'backend/src/Infrastructure/Rendering/VinylFilterGraphBuilder.cs',
    'utf8',
  )
  const constant = (name) =>
    Number(builder.match(new RegExp(`const int ${name} = ([0-9]+);`))[1])
  strictEqual(constant('GlowGreen'), Math.round(green * 0.14))
  strictEqual(constant('GlowBlue'), Math.round(blue * 0.14))
})

test('the bundle budget passes under the limit and fails over it', async () => {
  const { checkBudget } = await import('./bundle-budget.mjs')
  doesNotThrow(() => checkBudget(100, 130))
  throws(() => checkBudget(131, 130))
})

test('an export nothing imports is reported; an imported or test-imported one is not', async () => {
  const { findUnusedExports } = await import('./unused-exports.mjs')
  const { deepStrictEqual } = await import('node:assert/strict')
  const files = [
    {
      path: 'a.ts',
      text: 'export const used = 1\nexport function orphan() {}\nexport type Shape = {}',
    },
    { path: 'b.ts', text: "import { used } from './a'" },
    { path: 'a.test.ts', text: "import { type Shape } from './a'" },
  ]
  deepStrictEqual(findUnusedExports(files), ['a.ts: orphan'])
})

// A navigation the service worker does not exclude is answered with index.html, so a missing
// route turns a download into a saved HTML page. Every API route, under both prefixes the app
// is served with (bare in standalone mode, /api behind the dev proxy and nginx), must be denied.
test('the service worker never answers an API route with the app shell', async () => {
  const { readFileSync, readdirSync } = await import('node:fs')
  const { ok } = await import('node:assert/strict')
  const config = readFileSync('frontend/vite.config.ts', 'utf8')
  const list = config.match(/navigateFallbackDenylist:\s*\[([\s\S]*?)\]/)[1]
  const denylist = [...list.matchAll(/\/(\^[^\n]*?)\/,/g)].map(
    ([, source]) => new RegExp(source),
  )
  ok(denylist.length > 0, 'navigateFallbackDenylist was not found')

  const controllers = 'backend/src/Api/Controllers'
  const roots = readdirSync(controllers).flatMap((file) =>
    [
      ...readFileSync(`${controllers}/${file}`, 'utf8').matchAll(
        /\[Route\("([^"/]+)/g,
      ),
    ].map(([, root]) => root),
  )
  const program = readFileSync('backend/src/Api/Program.cs', 'utf8')
  for (const [, path] of program.matchAll(/MapHealthChecks\("\/([^"]+)"\)/g))
    roots.push(path)
  ok(roots.length >= 4, `expected the API's routes, found ${roots}`)

  for (const root of roots) {
    for (const path of [`/${root}/x/download`, `/api/${root}/x/download`]) {
      ok(
        denylist.some((pattern) => pattern.test(path)),
        `${path} is not in navigateFallbackDenylist`,
      )
    }
  }
})

test('the page sizes captions with the same ratio the video draws them at', async () => {
  const { readFileSync } = await import('node:fs')
  const { strictEqual } = await import('node:assert/strict')
  const builder = readFileSync(
    'backend/src/Infrastructure/Rendering/VinylFilterGraphBuilder.cs',
    'utf8',
  )
  const page = readFileSync('frontend/src/lib/captionFit.ts', 'utf8')
  strictEqual(
    Number(page.match(/const FONT_SIZE_RATIO = ([0-9.]+)/)[1]),
    Number(builder.match(/const double FontSizeRatio = ([0-9.]+);/)[1]),
  )
})

// A fake runner: `tools` says which media tools exist and how they answer; apt calls are recorded
// and answered from `apt` in order.
function fakeRunner({ tools, apt = [], installs = {} }) {
  const calls = []
  const run = (command, args, timeoutMs) => {
    calls.push({ command, args, timeoutMs })
    if (command === 'sudo') {
      const result = apt.shift() ?? { status: 1 }
      if (result.status === 0 && args.includes('install'))
        Object.assign(tools, installs)
      return { stdout: '', ...result }
    }
    if (command === 'dpkg-query') {
      const pkg = args.at(-1)
      return tools[pkg]
        ? { status: 0, stdout: 'install ok installed' }
        : { status: 1, stdout: '' }
    }
    const tool = tools[command]
    if (!tool) return { status: null, stdout: '', error: new Error('ENOENT') }
    if (args.includes('-decoders'))
      return { status: 0, stdout: tool.decoders ?? '' }
    return {
      status: tool.status ?? 0,
      stdout: tool.version ?? `${command} version 6.1`,
    }
  }
  return { run, calls }
}
const workingTools = () => ({
  ffmpeg: {
    decoders:
      ' VFS..D h264                 H.264\n A....D aac                  AAC\n',
  },
  ffprobe: {},
})
const ensureWith = async (runner, options = {}) => {
  const { ensureMediaTools } = await import('./ensure-ffmpeg.mjs')
  const sleeps = []
  const outcome = (() => {
    try {
      return ensureMediaTools({
        run: runner.run,
        sleep: (ms) => sleeps.push(ms),
        log: () => {},
        ...options,
      })
    } catch (error) {
      return error
    }
  })()
  return {
    outcome,
    sleeps,
    apt: runner.calls.filter((c) => c.command === 'sudo'),
  }
}

test('working preinstalled ffmpeg and ffprobe are reused without touching apt', async () => {
  const { strictEqual } = await import('node:assert/strict')
  const { outcome, apt } = await ensureWith(
    fakeRunner({ tools: workingTools() }),
  )
  strictEqual(outcome, 'preinstalled')
  strictEqual(apt.length, 0)
})

test('missing or broken tools are installed with bounded apt calls, then checked again', async () => {
  const { strictEqual, ok } = await import('node:assert/strict')
  const broken = [
    {}, // nothing installed
    { ffmpeg: workingTools().ffmpeg }, // ffprobe missing
    { ...workingTools(), ffprobe: { status: 1 } }, // ffprobe present but fails
    { ...workingTools(), ffmpeg: { decoders: ' VFS..D vp9  VP9\n' } }, // no H.264/AAC decoder
  ]
  for (const tools of broken) {
    const runner = fakeRunner({
      tools,
      apt: [{ status: 0 }, { status: 0 }],
      installs: workingTools(),
    })
    const { outcome, apt } = await ensureWith(runner)
    strictEqual(outcome, 'installed')
    strictEqual(apt.length, 2)
    const [update, install] = apt
    ok(update.args.includes('update') && update.args.includes('--error-on=any'))
    ok(install.args.includes('install') && install.args.includes('ffmpeg'))
    for (const call of apt) {
      ok(call.timeoutMs > 0 && call.timeoutMs <= 5 * 60_000)
      ok(call.args.includes('Acquire::http::Timeout=20'))
      ok(call.args.includes('Acquire::Retries=2'))
    }
  }
})

test('a failing mirror stops after the bounded attempts and fails loudly instead of hanging', async () => {
  const { strictEqual, ok, deepStrictEqual } =
    await import('node:assert/strict')
  // Update fails like the DNS outage, then times out; install must never be attempted.
  const runner = fakeRunner({
    tools: {},
    apt: [{ status: 100 }, { status: null, timedOut: true }, { status: 0 }],
  })
  const { outcome, apt, sleeps } = await ensureWith(runner)
  ok(outcome instanceof Error && /after 2 attempts/.test(outcome.message))
  strictEqual(apt.length, 2)
  ok(apt.every((c) => c.args.includes('update')))
  deepStrictEqual(sleeps, [15_000])
})

test('apt that reports success but leaves unusable tools is a failure, not a pass', async () => {
  const { ok } = await import('node:assert/strict')
  const runner = fakeRunner({
    tools: {},
    apt: [{ status: 0 }, { status: 0 }],
    installs: { ffmpeg: workingTools().ffmpeg },
  })
  const { outcome } = await ensureWith(runner)
  ok(outcome instanceof Error && /ffprobe/.test(outcome.message))
})

test('extra apt packages are part of "already installed" and of the install', async () => {
  const { strictEqual, ok } = await import('node:assert/strict')
  const present = await ensureWith(
    fakeRunner({ tools: { ...workingTools(), 'fonts-dejavu-core': true } }),
    { extraPackages: ['fonts-dejavu-core'] },
  )
  strictEqual(present.outcome, 'preinstalled')
  const absent = await ensureWith(
    fakeRunner({
      tools: workingTools(),
      apt: [{ status: 0 }, { status: 0 }],
      installs: { 'fonts-dejavu-core': true },
    }),
    { extraPackages: ['fonts-dejavu-core'] },
  )
  strictEqual(absent.outcome, 'installed')
  ok(absent.apt[1].args.includes('fonts-dejavu-core'))
})

test('this machine passes the same tool check the runner uses', async () => {
  const { checkMediaTools } = await import('./ensure-ffmpeg.mjs')
  const { deepStrictEqual } = await import('node:assert/strict')
  const { spawnSync } = await import('node:child_process')
  const run = (command, args) => {
    const r = spawnSync(command, args, { encoding: 'utf8', timeout: 20_000 })
    return { status: r.status, stdout: r.stdout ?? '', error: r.error }
  }
  // verify.mjs already requires both tools, so a real binary must satisfy the real check.
  deepStrictEqual(checkMediaTools(run), [])
  const missing = checkMediaTools((c, a) => run(`${c}-definitely-missing`, a))
  deepStrictEqual(missing, [
    'ffmpeg is missing or does not run',
    'ffprobe is missing or does not run',
  ])
})

test('both workflows set up ffmpeg through the bounded script and keep the real checks', async () => {
  const { readFileSync } = await import('node:fs')
  const { ok, match, doesNotMatch } = await import('node:assert/strict')
  for (const file of ['ci.yml', 'verify-deployment.yml']) {
    const text = readFileSync(`.github/workflows/${file}`, 'utf8')
    doesNotMatch(text, /apt-get/, `${file} must not call apt directly`)
    match(
      text,
      /timeout-minutes: \d+\n\s+run: node scripts\/ensure-ffmpeg\.mjs/,
      `${file} must bound the setup step`,
    )
    doesNotMatch(text, /continue-on-error/, `${file} must not hide a failure`)
    for (const [, ref] of text.matchAll(/uses: \S+@(\S+)/g)) {
      ok(
        /^[0-9a-f]{40}$/.test(ref),
        `${file}: action not pinned by SHA: ${ref}`,
      )
    }
  }
  const live = readFileSync('.github/workflows/verify-deployment.yml', 'utf8')
  match(
    live,
    /bash scripts\/verify-deployment\.sh "\$PRODUCTION_URL" "\$EXPECTED_SHA"/,
  )
  doesNotMatch(
    live,
    /\n\s+if:/,
    'the live verification must not be conditional',
  )
})
