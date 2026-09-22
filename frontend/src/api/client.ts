const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export type VideoPreset = '1080p' | '720p'
export type CaptionFont = 'sans-bold' | 'serif-bold' | 'mono-bold'

export interface CreateJobRequest {
  title: string
  preset: VideoPreset
  rotationSpeedSeconds: number
  captionFont: CaptionFont
  audioFile: File
  artworkFile: File
}

export interface CreateJobResponse {
  jobId: string
}

export type JobStatusValue = 'Queued' | 'Processing' | 'Completed' | 'Failed'

export interface JobStatusResponse {
  jobId: string
  status: JobStatusValue
  progress: number
  errorMessage: string | null
}

async function parseErrorDetail(response: Response): Promise<string> {
  try {
    const body = await response.json()
    if (typeof body?.detail === 'string' && body.detail.length > 0) {
      return body.detail
    }
  } catch {
    // response body wasn't JSON (or was empty) - fall through to the generic message
  }
  return `Request failed with status ${response.status}.`
}

export async function createJob(
  request: CreateJobRequest,
): Promise<CreateJobResponse> {
  const formData = new FormData()
  formData.set('Title', request.title)
  formData.set('Preset', request.preset)
  formData.set('RotationSpeedSeconds', String(request.rotationSpeedSeconds))
  formData.set('CaptionFont', request.captionFont)
  formData.set('Audio', request.audioFile)
  formData.set('Artwork', request.artworkFile)

  const response = await fetch(`${BASE_URL}/jobs`, {
    method: 'POST',
    body: formData,
  })
  if (!response.ok) {
    throw new ApiError(await parseErrorDetail(response), response.status)
  }

  return (await response.json()) as CreateJobResponse
}

export async function getJobStatus(jobId: string): Promise<JobStatusResponse> {
  const response = await fetch(`${BASE_URL}/jobs/${encodeURIComponent(jobId)}`)
  if (!response.ok) {
    throw new ApiError(await parseErrorDetail(response), response.status)
  }

  return (await response.json()) as JobStatusResponse
}

export function downloadUrl(jobId: string): string {
  return `${BASE_URL}/jobs/${encodeURIComponent(jobId)}/download`
}

export interface CreateSampleJobRequest {
  title?: string
  preset?: VideoPreset
  rotationSpeedSeconds?: number
  captionFont?: CaptionFont
}

/**
 * Asks the server to render its own bundled sample mix, so a first-time visitor can watch the
 * pipeline run without finding a DJ set and waiting on a large upload. Enabled per deployment;
 * a server with the demo switched off answers 404, which surfaces as a normal ApiError.
 */
export async function createSampleJob(
  request: CreateSampleJobRequest = {},
): Promise<CreateJobResponse> {
  const response = await fetch(`${BASE_URL}/jobs/sample`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) {
    throw new ApiError(await parseErrorDetail(response), response.status)
  }

  return (await response.json()) as CreateJobResponse
}

/**
 * True when this build was made for a deployment that bundles the sample assets. A function
 * rather than a module-level constant so it is readable at call time - a constant would freeze
 * the value at import and could not be exercised in tests.
 */
export function isSampleMixEnabled(): boolean {
  return import.meta.env.VITE_SAMPLE_ENABLED === 'true'
}

/**
 * The commit this bundle was built from, injected at build time. Shown in the footer so a
 * deployed page can be tied back to a commit without guessing which deploy is live.
 */
export function buildSha(): string | null {
  const sha = import.meta.env.VITE_BUILD_SHA
  return sha && sha !== 'unknown' ? sha : null
}

export interface UploadLimits {
  maxAudioBytes: number
  maxImageBytes: number
  maxDurationSeconds: number
}

/**
 * The instance's effective upload limits. Fetched rather than hardcoded because they differ by
 * an order of magnitude between a self-hosted instance and the free-tier demo, and a UI that
 * stated the wrong ones would promise uploads the server rejects.
 */
export async function getLimits(): Promise<UploadLimits> {
  const response = await fetch(`${BASE_URL}/limits`)
  if (!response.ok) {
    throw new ApiError(await parseErrorDetail(response), response.status)
  }

  return (await response.json()) as UploadLimits
}

/**
 * The commit this instance is running, resolved at runtime by the server. Null when the host
 * exposes no commit - the footer then simply does not render.
 */
export async function getVersion(): Promise<{ commit: string | null }> {
  const response = await fetch(`${BASE_URL}/version`)
  if (!response.ok) {
    throw new ApiError(await parseErrorDetail(response), response.status)
  }

  return (await response.json()) as { commit: string | null }
}

export const previewUrl = (jobId: string) =>
  `${BASE_URL}/jobs/${encodeURIComponent(jobId)}/preview`
