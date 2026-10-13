import { expect, test, type Page } from '@playwright/test'

// The API is mocked so every screen is reachable in a second, with fixed values - a real
// render's timing and progress would make each screenshot different.
const JOB = 'visual-job'
const BASE = {
  jobId: JOB,
  errorMessage: null,
  downloadsRemaining: 4,
  preset: '1080p',
  captionFont: 'serif-bold',
  rotationSpeedSeconds: 18,
}

async function mockApi(page: Page, status: Record<string, unknown>) {
  await page.route('**/api/limits', (route) =>
    route.fulfill({
      json: {
        maxAudioBytes: 2_000_000_000,
        maxImageBytes: 25_000_000,
        maxDurationSeconds: 21_600,
      },
    }),
  )
  await page.route('**/api/version', (route) =>
    route.fulfill({ json: { commit: null } }),
  )
  await page.route(`**/api/jobs/${JOB}`, (route) =>
    route.fulfill({ json: { ...BASE, ...status } }),
  )
}

for (const colorScheme of ['dark', 'light'] as const) {
  test.describe(colorScheme, () => {
    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' })
    })

    test('upload form', async ({ page }) => {
      await mockApi(page, { status: 'Queued', progress: 0 })
      await page.goto('/')
      await expect(
        page.getByRole('button', { name: 'Generate Video' }),
      ).toBeVisible()
      await expect(page).toHaveScreenshot(`form-${colorScheme}.png`, {
        fullPage: true,
      })
    })

    test('rendering', async ({ page }) => {
      await mockApi(page, { status: 'Processing', progress: 42 })
      await page.goto(`/?job=${JOB}`)
      await expect(page.getByText('42%')).toBeVisible()
      // The elapsed clock depends on wall time; everything else on the screen is fixed.
      await expect(page).toHaveScreenshot(`rendering-${colorScheme}.png`, {
        mask: [page.getByText(/elapsed/)],
      })
    })

    test('finished', async ({ page }) => {
      await mockApi(page, { status: 'Completed', progress: 100 })
      await page.route(`**/api/jobs/${JOB}/**`, (route) => route.abort())
      await page.goto(`/?job=${JOB}`)
      await expect(page.getByText('Your video is ready.')).toBeVisible()
      await expect(page).toHaveScreenshot(`finished-${colorScheme}.png`, {
        mask: [page.locator('video')],
      })
    })

    test('failed', async ({ page }) => {
      await mockApi(page, {
        status: 'Failed',
        progress: 30,
        errorMessage: 'The render could not finish.',
      })
      await page.goto(`/?job=${JOB}`)
      await expect(page.getByText('Rendering failed')).toBeVisible()
      await expect(page).toHaveScreenshot(`failed-${colorScheme}.png`)
    })
  })
}
