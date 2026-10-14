// zod/mini rather than zod: the same validation without the chainable classic API, which kept
// most of zod's code in the entry chunk.
import { z } from 'zod/mini'

export const MIN_ROTATION_SPEED_SECONDS = 12
export const MAX_ROTATION_SPEED_SECONDS = 90
export const DEFAULT_ROTATION_SPEED_SECONDS = 18

const CAPTION_FONTS = ['sans-bold', 'serif-bold', 'mono-bold'] as const

export const renderSettingsSchema = z.object({
  // Optional: a blank title renders the record with no caption under it.
  title: z._default(
    z
      .string()
      .check(
        z.trim(),
        z.maxLength(200, 'Title must be 200 characters or fewer.'),
      ),
    '',
  ),
  preset: z.enum(['1080p', '720p'], { error: 'Choose a video preset.' }),
  rotationSpeedSeconds: z._default(
    z
      .number()
      .check(
        z.gte(
          MIN_ROTATION_SPEED_SECONDS,
          `Rotation speed must be at least ${MIN_ROTATION_SPEED_SECONDS}s.`,
        ),
        z.lte(
          MAX_ROTATION_SPEED_SECONDS,
          `Rotation speed must be at most ${MAX_ROTATION_SPEED_SECONDS}s.`,
        ),
      ),
    DEFAULT_ROTATION_SPEED_SECONDS,
  ),
  captionFont: z._default(
    z.enum(CAPTION_FONTS, { error: 'Choose a caption font.' }),
    'sans-bold',
  ),
})

export type RenderSettingsValues = z.output<typeof renderSettingsSchema>
export type RenderSettingsInput = z.input<typeof renderSettingsSchema>
