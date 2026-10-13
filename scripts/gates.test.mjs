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
