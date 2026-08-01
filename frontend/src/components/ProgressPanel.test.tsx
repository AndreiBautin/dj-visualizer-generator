import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import * as apiClient from '../api/client'
import { ProgressPanel } from './ProgressPanel'

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}

describe('ProgressPanel', () => {
  it('shows a queued message while the job is waiting', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Queued',
      progress: 0,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByText(/queued/i)).toBeInTheDocument()
  })

  it('shows the render progress percentage while processing', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Processing',
      progress: 42,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByText('42%')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '42')
  })

  it('shows a download link when the job has completed', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Completed',
      progress: 100,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByRole('link', { name: /download/i })).toHaveAttribute(
      'href',
      apiClient.downloadUrl('abc'),
    )
  })

  it('shows the error message when the job has failed', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Failed',
      progress: 0,
      errorMessage: 'ffmpeg exited with code 1',
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByText('ffmpeg exited with code 1')).toBeInTheDocument()
  })
})
