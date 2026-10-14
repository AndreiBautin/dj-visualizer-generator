import { useState } from 'react'
import { useWatch } from 'react-hook-form'
import { useJobStatus } from '../hooks/useJobStatus'
import { CAPTION_FONT_OPTIONS } from '../lib/captionFonts'
import type { LabelCrop } from '../lib/labelCrop'
import {
  DEFAULT_ROTATION_SPEED_SECONDS,
  type RenderSettingsInput,
} from '../schemas/renderSettingsSchema'
import { VinylRecord } from './VinylRecord'

/** Isolates the one field VinylRecord needs, so dragging the rotation-speed slider re-renders
 * just the record rather than the whole two-column layout. */
export function LiveVinylRecord({
  artworkFile,
  motion,
  celebrate,
  jobSettings,
  labelCrop,
}: {
  artworkFile: File | null
  motion?: 'idle' | 'rendering' | 'stopped'
  celebrate?: boolean
  /** A job's own settings win over the form's - after a reload the form holds defaults. */
  jobSettings?: { rotationSpeedSeconds: number; captionFont: string }
  labelCrop?: LabelCrop
}) {
  const formSpeed = useWatch<RenderSettingsInput>({
    name: 'rotationSpeedSeconds',
  })
  const rotationSpeedSeconds = jobSettings?.rotationSpeedSeconds ?? formSpeed
  const title = useWatch<RenderSettingsInput>({ name: 'title' })
  const formFont = useWatch<RenderSettingsInput>({ name: 'captionFont' })
  const captionFont = jobSettings?.captionFont ?? formFont
  const caption = typeof title === 'string' ? title.trim() : ''
  const fontFamily = CAPTION_FONT_OPTIONS.find(
    (option) => option.value === captionFont,
  )?.previewFontFamily
  return (
    <div className="flex flex-col items-center gap-4">
      <VinylRecord
        artworkFile={artworkFile}
        rotationSpeedSeconds={
          typeof rotationSpeedSeconds === 'number'
            ? rotationSpeedSeconds
            : DEFAULT_ROTATION_SPEED_SECONDS
        }
        motion={motion}
        celebrate={celebrate}
        labelCrop={labelCrop}
      />
      {/* Where and how the render's drawtext stage burns the title: centred under the
          record, white with a soft shadow, in the chosen caption face. */}
      <p
        aria-hidden="true"
        className={`max-w-full truncate text-center text-xl text-white transition-opacity duration-300 [text-shadow:2px_2px_0_rgb(0_0_0/0.5)] ${caption ? 'opacity-100' : 'opacity-0'}`}
        style={{ fontFamily }}
      >
        {caption || ' '}
      </p>
    </div>
  )
}

/** Shares the progress panel's polling query (same key), so this adds no requests. */
export function JobVinylRecord({
  jobId,
  artworkFile,
}: {
  jobId: string
  artworkFile: File | null
}) {
  const { data } = useJobStatus(jobId)
  // Celebrate a job seen finishing, not one reopened already finished from a link.
  const [sawInProgress, setSawInProgress] = useState(false)
  if (
    !sawInProgress &&
    (data?.status === 'Queued' || data?.status === 'Processing')
  ) {
    setSawInProgress(true)
  }
  const motion =
    data?.status === 'Processing'
      ? 'rendering'
      : data?.status === 'Completed' || data?.status === 'Failed'
        ? 'stopped'
        : 'idle'
  return (
    <LiveVinylRecord
      artworkFile={artworkFile}
      motion={motion}
      celebrate={sawInProgress && data?.status === 'Completed'}
      jobSettings={data}
      labelCrop={
        data
          ? {
              focusX: data.labelFocusX,
              focusY: data.labelFocusY,
              zoom: data.labelZoom,
            }
          : undefined
      }
    />
  )
}
