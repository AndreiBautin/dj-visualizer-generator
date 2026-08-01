import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { downloadUrl } from '../api/client'
import { DownloadPanel } from './DownloadPanel'

describe('DownloadPanel', () => {
  it('renders a download link pointing at the job download endpoint', () => {
    render(<DownloadPanel jobId="abc-123" onReset={vi.fn()} />)

    const link = screen.getByRole('link', { name: /download/i })
    expect(link).toHaveAttribute('href', downloadUrl('abc-123'))
  })

  it('calls onReset when starting another video', async () => {
    const onReset = vi.fn()
    const user = userEvent.setup()
    render(<DownloadPanel jobId="abc-123" onReset={onReset} />)

    await user.click(screen.getByRole('button', { name: /another/i }))

    expect(onReset).toHaveBeenCalledOnce()
  })
})
