import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, createJob, downloadUrl, getJobStatus } from './client'

function makeFile(name: string): File {
  return new File([new Uint8Array(4)], name)
}

describe('createJob', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts multipart form data and returns the job id', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ jobId: 'abc-123' }), { status: 201 }),
      )
    vi.stubGlobal('fetch', fetchMock)

    const result = await createJob({
      title: 'Friday Night Set',
      preset: '1080p',
      rotationSpeedSeconds: 4.5,
      captionFont: 'mono-bold',
      audioFile: makeFile('mix.mp3'),
      artworkFile: makeFile('cover.png'),
    })

    expect(result).toEqual({ jobId: 'abc-123' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/jobs')
    expect(init.method).toBe('POST')
    expect(init.body).toBeInstanceOf(FormData)
    const formData = init.body as FormData
    expect(formData.get('RotationSpeedSeconds')).toBe('4.5')
    expect(formData.get('CaptionFont')).toBe('mono-bold')
  })

  it('throws an ApiError with the problem detail message on failure', async () => {
    const fetchMock = vi.fn().mockImplementation(
      async () =>
        new Response(JSON.stringify({ detail: 'Title must not be empty.' }), {
          status: 400,
        }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const request = {
      title: '',
      preset: '1080p' as const,
      rotationSpeedSeconds: 3,
      captionFont: 'sans-bold' as const,
      audioFile: makeFile('mix.mp3'),
      artworkFile: makeFile('cover.png'),
    }
    await expect(createJob(request)).rejects.toThrow(ApiError)
    await expect(createJob(request)).rejects.toThrow('Title must not be empty.')
  })
})

describe('getJobStatus', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns the parsed job status', async () => {
    const body = {
      jobId: 'abc-123',
      status: 'Processing',
      progress: 42,
      errorMessage: null,
      downloadsRemaining: 5,
      preset: '1080p',
      captionFont: 'sans-bold',
      rotationSpeedSeconds: 18,
      labelFocusX: 0.5,
      labelFocusY: 0.5,
      labelZoom: 1,
      title: 'Set',
    }
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(new Response(JSON.stringify(body), { status: 200 })),
    )

    const result = await getJobStatus('abc-123')

    expect(result).toEqual(body)
  })

  // The status drives which screen is shown; one the page does not know must not be trusted.
  it.each([
    ['an unknown status', { ...STATUS, status: 'Exploded' }],
    ['a missing field', { jobId: 'abc-123', status: 'Processing' }],
    ['a body that is not JSON', 'oops'],
  ])('rejects %s instead of passing it on', async (_, body) => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(typeof body === 'string' ? body : JSON.stringify(body), {
          status: 200,
        }),
      ),
    )

    await expect(getJobStatus('abc-123')).rejects.toBeInstanceOf(ApiError)
  })

  it('throws an ApiError when the job is not found', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ detail: 'No job exists.' }), {
          status: 404,
        }),
      ),
    )

    await expect(getJobStatus('missing')).rejects.toThrow(ApiError)
  })
})

describe('downloadUrl', () => {
  it('builds the download URL for a job id', () => {
    expect(downloadUrl('abc-123')).toBe('/api/jobs/abc-123/download')
  })
})

const STATUS = {
  jobId: 'abc-123',
  status: 'Processing',
  progress: 42,
  errorMessage: null,
  downloadsRemaining: 5,
  preset: '1080p',
  captionFont: 'sans-bold',
  rotationSpeedSeconds: 18,
  labelFocusX: 0.5,
  labelFocusY: 0.5,
  labelZoom: 1,
  title: 'Set',
}
