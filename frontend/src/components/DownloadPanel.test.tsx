import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { downloadUrl, previewUrl } from '../api/client'
import { DownloadPanel } from './DownloadPanel'

describe('DownloadPanel', () => {
  it('renders a download link pointing at the job download endpoint', () => {
    render(<DownloadPanel jobId="abc-123" onReset={vi.fn()} />)

    const link = screen.getByRole('link', { name: /download/i })
    expect(link).toHaveAttribute('href', downloadUrl('abc-123'))
  })

  it('renders an inline player pointing at the separate preview endpoint', () => {
    render(<DownloadPanel jobId="abc-123" onReset={vi.fn()} />)

    const video = document.querySelector('video')
    expect(video).toHaveAttribute('src', previewUrl('abc-123'))
    expect(video).toHaveAttribute('controls')
  })

  it('does not fetch the video until the visitor presses play', () => {
    render(<DownloadPanel jobId="abc-123" onReset={vi.fn()} />)

    // The download endpoint is rate-limited and counts against the job's small download
    // allowance (Job.MaxDownloads) - mounting the player must not itself spend one.
    expect(document.querySelector('video')).toHaveAttribute('preload', 'none')
  })

  it('animates in on mount, for visitors who have not asked for reduced motion', () => {
    const { container } = render(
      <DownloadPanel jobId="abc-123" onReset={vi.fn()} />,
    )

    expect(container.firstChild).toHaveClass(
      'motion-safe:animate-[panel-in_220ms_ease-out]',
    )
  })

  it('calls onReset when starting another video', async () => {
    const onReset = vi.fn()
    const user = userEvent.setup()
    render(<DownloadPanel jobId="abc-123" onReset={onReset} />)

    await user.click(screen.getByRole('button', { name: /another/i }))

    expect(onReset).toHaveBeenCalledOnce()
  })
})
