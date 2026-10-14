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
