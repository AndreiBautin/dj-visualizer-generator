import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

// Both themes, because contrast is the rule most likely to break and the light theme flips
// every text and border token.
for (const colorScheme of ['dark', 'light'] as const) {
  test(`the upload form has no detectable accessibility violations (${colorScheme})`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' })
    await page.goto('/')
    await expect(
      page.getByRole('button', { name: 'Generate Video' }),
    ).toBeVisible()

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze()

    expect(results.violations).toEqual([])
  })
}
