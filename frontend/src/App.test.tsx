import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import App from './App'
import * as apiClient from './api/client'

function makeFile(name: string, type = ''): File {
  return new File([new Uint8Array(4)], name, { type })
}

function renderApp() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>,
  )
}

describe('App', () => {
  it('renders the app title', () => {
    renderApp()

    expect(screen.getByRole('heading', { name: /dj visualizer generator/i })).toBeInTheDocument()
  })

  it('renders the upload form with both file pickers and the settings fields', () => {
    renderApp()

    expect(screen.getByText('Audio file')).toBeInTheDocument()
    expect(screen.getByText('Artwork')).toBeInTheDocument()
    expect(screen.getByLabelText(/track title/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /generate video/i })).toBeDisabled()
  })

  it('enables the submit button once both files are selected', async () => {
    const user = userEvent.setup()
    renderApp()

    await user.upload(screen.getByLabelText('Audio file', { selector: 'input' }), makeFile('mix.mp3'))
    await user.upload(screen.getByLabelText('Artwork', { selector: 'input' }), makeFile('cover.png', 'image/png'))

    expect(screen.getByRole('button', { name: /generate video/i })).toBeEnabled()
  })

  it('creates a job and shows the progress panel on submit', async () => {
    vi.spyOn(apiClient, 'createJob').mockResolvedValue({ jobId: 'abc-123' })
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc-123',
      status: 'Queued',
      progress: 0,
      errorMessage: null,
    })
    const user = userEvent.setup()
    renderApp()

    await user.type(screen.getByLabelText(/track title/i), 'Friday Night Set')
    await user.upload(screen.getByLabelText('Audio file', { selector: 'input' }), makeFile('mix.mp3'))
    await user.upload(screen.getByLabelText('Artwork', { selector: 'input' }), makeFile('cover.png', 'image/png'))
    await user.click(screen.getByRole('button', { name: /generate video/i }))

    expect(await screen.findByText(/queued/i)).toBeInTheDocument()
    expect(apiClient.createJob).toHaveBeenCalledWith(
      {
        title: 'Friday Night Set',
        preset: '1080p',
        rotationSpeedSeconds: 3,
        captionFont: 'sans-bold',
        audioFile: expect.any(File),
        artworkFile: expect.any(File),
      },
      expect.anything(),
    )
  })
})
