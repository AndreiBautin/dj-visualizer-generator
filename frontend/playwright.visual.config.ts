import { defineConfig, devices } from '@playwright/test'

// Screenshot baselines are font-rendering specific, so they are recorded per platform and run
// locally (npm run test:visual), not in CI's Linux job - see docs/TESTING.md.
export default defineConfig({
  testDir: './visual',
  reporter: 'line',
  snapshotPathTemplate: '{testDir}/__screenshots__/{platform}/{arg}{ext}',
  expect: {
    toHaveScreenshot: { maxDiffPixelRatio: 0.01, animations: 'disabled' },
  },
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    viewport: { width: 1280, height: 860 },
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 860 },
      },
    },
  ],
})
