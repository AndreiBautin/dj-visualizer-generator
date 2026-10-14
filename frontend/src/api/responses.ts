// What the API is allowed to send back, checked at the boundary. The responses used to be cast
// with `as`, so an unexpected status string or a missing field went straight into the screens'
// branches; now anything off-shape becomes one ApiError the UI already knows how to show.
import { z } from 'zod/mini'

const videoPreset = z.enum(['1080p', '720p'])
const captionFont = z.enum(['sans-bold', 'serif-bold', 'mono-bold'])

const jobStatusValues = ['Queued', 'Processing', 'Completed', 'Failed'] as const

export const createJobResponse = z.object({ jobId: z.string() })

export const jobStatusResponse = z.object({
  jobId: z.string(),
  status: z.enum(jobStatusValues),
  progress: z.number(),
  errorMessage: z.nullable(z.string()),
  /** Saves left of the job's five; previewing does not spend them. */
  downloadsRemaining: z.number(),
  preset: videoPreset,
  captionFont: captionFont,
  rotationSpeedSeconds: z.number(),
  labelFocusX: z.number(),
  labelFocusY: z.number(),
  labelZoom: z.number(),
  /** Empty when the job has no caption. */
  title: z.string(),
})

export const uploadLimits = z.object({
  maxAudioBytes: z.number(),
  maxImageBytes: z.number(),
  maxDurationSeconds: z.number(),
})

export const versionResponse = z.object({ commit: z.nullable(z.string()) })
