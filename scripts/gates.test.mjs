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
