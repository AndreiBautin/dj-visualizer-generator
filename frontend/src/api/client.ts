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

export async function createJob(request: CreateJobRequest): Promise<CreateJobResponse> {
  const formData = new FormData()
  formData.set('Title', request.title)
  formData.set('Preset', request.preset)
  formData.set('RotationSpeedSeconds', String(request.rotationSpeedSeconds))
  formData.set('CaptionFont', request.captionFont)
  formData.set('Audio', request.audioFile)
  formData.set('Artwork', request.artworkFile)

  const response = await fetch(`${BASE_URL}/jobs`, { method: 'POST', body: formData })
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
