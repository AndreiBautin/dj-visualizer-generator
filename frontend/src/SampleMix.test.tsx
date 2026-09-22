import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import * as apiClient from './api/client'

function renderApp() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>,
  )
}

describe('sample mix', () => {
  beforeEach(() => {
    vi.spyOn(apiClient, 'getLimits').mockResolvedValue({
      maxAudioBytes: 62_914_560,
      maxImageBytes: 10_485_760,
      maxDurationSeconds: 900,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('hides the sample button when the build did not enable it', () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(false)
    renderApp()

    expect(
      screen.queryByRole('button', { name: /render a sample mix/i }),
    ).not.toBeInTheDocument()
  })

  it('offers the sample button when the build enabled it', () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(true)
    renderApp()

    expect(
      screen.getByRole('button', { name: /render a sample mix/i }),
    ).toBeEnabled()
  })

  it('starts a sample job without requiring any file to be chosen', async () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(true)
    const createSampleJob = vi
      .spyOn(apiClient, 'createSampleJob')
      .mockResolvedValue({ jobId: 'job-1' })
    const user = userEvent.setup()
    renderApp()

    await user.click(
      screen.getByRole('button', { name: /render a sample mix/i }),
    )

    expect(createSampleJob).toHaveBeenCalledTimes(1)
    // The upload form requires two files; the sample path must not.
    await waitFor(() =>
      expect(screen.queryByText('Audio file')).not.toBeInTheDocument(),
    )
  })

  it('sends the render settings currently in the form', async () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(true)
    const createSampleJob = vi
      .spyOn(apiClient, 'createSampleJob')
      .mockResolvedValue({ jobId: 'job-2' })
    const user = userEvent.setup()
    renderApp()

    await user.type(screen.getByLabelText(/track title/i), 'My Sample')
    await user.click(
      screen.getByRole('button', { name: /render a sample mix/i }),
    )

    // Asserting on the first argument only: react-query passes its own context as a second one.
    expect(createSampleJob.mock.calls[0][0]).toEqual({
      title: 'My Sample',
      preset: '1080p',
      rotationSpeedSeconds: 3,
      captionFont: 'sans-bold',
    })
  })

  it('surfaces a server that has the sample switched off', async () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(true)
    vi.spyOn(apiClient, 'createSampleJob').mockRejectedValue(
      new apiClient.ApiError(
        'The sample mix is not enabled on this instance.',
        404,
      ),
    )
    const user = userEvent.setup()
    renderApp()

    await user.click(
      screen.getByRole('button', { name: /render a sample mix/i }),
    )

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /not enabled on this instance/i,
    )
  })
})

describe('upload limits', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  /**
   * The deployed demo accepts far smaller uploads than a self-hosted instance. The hint text has
   * to come from the server, or the UI would invite uploads the server then rejects.
   */
  it('states the limits reported by the server rather than the built-in defaults', async () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(false)
    vi.spyOn(apiClient, 'getLimits').mockResolvedValue({
      maxAudioBytes: 62_914_560,
      maxImageBytes: 10_485_760,
      maxDurationSeconds: 900,
    })
    renderApp()

    expect(
      await screen.findByText(/up to 60 MB, up to 15 minutes/i),
    ).toBeInTheDocument()
    expect(screen.getByText(/JPG or PNG \(up to 10 MB\)/i)).toBeInTheDocument()
  })

  it('falls back to the self-hosted defaults when the limits cannot be fetched', async () => {
    vi.spyOn(apiClient, 'isSampleMixEnabled').mockReturnValue(false)
    vi.spyOn(apiClient, 'getLimits').mockRejectedValue(
      new apiClient.ApiError('nope', 500),
    )
    renderApp()

    expect(
      await screen.findByText(/up to 2\.0 GB, up to 6 hours/i),
    ).toBeInTheDocument()
  })
})
