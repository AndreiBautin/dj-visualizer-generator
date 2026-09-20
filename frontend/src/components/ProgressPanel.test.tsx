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
  it('shows a checking-in message before the first status arrives', () => {
    vi.spyOn(apiClient, 'getJobStatus').mockReturnValue(new Promise(() => {}))

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(screen.getByText('Checking on your render...')).toBeInTheDocument()
  })

  it('shows a reassuring retry message if a status check fails', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockRejectedValue(new Error('network down'))

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByText(/lost the connection for a moment.*retrying/i)).toBeInTheDocument()
  })

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

  it('does not repeat the same "waiting for a worker" detail in both the heading and the caption', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Queued',
      progress: 0,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    expect(await screen.findByText('Queued')).toBeInTheDocument()
    expect(screen.getByText('Waiting for a worker to pick this up...')).toBeInTheDocument()
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

  it('renders the queued bar as indeterminate rather than stuck at 0%', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Queued',
      progress: 0,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    await screen.findByText(/queued/i)
    // No aria-valuenow is the correct ARIA pattern for an indeterminate progressbar - asserting
    // "0" here would tell assistive tech the job is precisely 0% done, which is what made the
    // stuck-at-0% bar read as frozen rather than waiting in the first place.
    expect(screen.getByRole('progressbar')).not.toHaveAttribute('aria-valuenow')
  })

  it('sets the document title to the live render progress while processing', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Processing',
      progress: 42,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    await screen.findByText('42%')
    expect(document.title).toBe('42% · DJ Visualizer Generator')
  })

  it('sets a queued document title while waiting for a worker', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Queued',
      progress: 0,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    await screen.findByText(/queued/i)
    expect(document.title).toBe('Queued · DJ Visualizer Generator')
  })

  it('restores the default document title once the job completes, instead of leaving the last percentage shown', async () => {
    // Testing Library's automatic afterEach unmount would reset the title on its own and mask
    // this bug, so the stale value is forced in directly rather than relying on a prior render
    // to have left it behind - this must be corrected by the Completed-status render itself,
    // while the component is still mounted, the way a real 99% -> Completed transition would.
    document.title = '99% · DJ Visualizer Generator'
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Completed',
      progress: 100,
      errorMessage: null,
    })

    renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)

    await screen.findByRole('link', { name: /download/i })
    expect(document.title).toBe('DJ Visualizer Generator')
  })

  it('restores the default document title on unmount', async () => {
    vi.spyOn(apiClient, 'getJobStatus').mockResolvedValue({
      jobId: 'abc',
      status: 'Processing',
      progress: 42,
      errorMessage: null,
    })

    const { unmount } = renderWithClient(<ProgressPanel jobId="abc" onReset={vi.fn()} />)
    await screen.findByText('42%')
    unmount()

    expect(document.title).toBe('DJ Visualizer Generator')
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
