import { z } from 'zod'

export const MIN_ROTATION_SPEED_SECONDS = 12
export const MAX_ROTATION_SPEED_SECONDS = 90
export const DEFAULT_ROTATION_SPEED_SECONDS = 18

export const CAPTION_FONTS = ['sans-bold', 'serif-bold', 'mono-bold'] as const

export const renderSettingsSchema = z.object({
  title: z
    .string()
    .trim()
    // Optional: a blank title renders the record with no caption under it.
    .max(200, 'Title must be 200 characters or fewer.')
    .default(''),
  preset: z.enum(['1080p', '720p'], { message: 'Choose a video preset.' }),
  rotationSpeedSeconds: z
    .number()
    .min(
      MIN_ROTATION_SPEED_SECONDS,
      `Rotation speed must be at least ${MIN_ROTATION_SPEED_SECONDS}s.`,
    )
    .max(
      MAX_ROTATION_SPEED_SECONDS,
      `Rotation speed must be at most ${MAX_ROTATION_SPEED_SECONDS}s.`,
    )
    .default(DEFAULT_ROTATION_SPEED_SECONDS),
  captionFont: z
    .enum(CAPTION_FONTS, { message: 'Choose a caption font.' })
    .default('sans-bold'),
})

export type RenderSettingsValues = z.output<typeof renderSettingsSchema>
export type RenderSettingsInput = z.input<typeof renderSettingsSchema>
export type CaptionFont = (typeof CAPTION_FONTS)[number]
