import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { SURFACE_RAISED } from '../lib/surfaces'
import { FilePreview } from './FilePreview'

function makeFile(name: string, size: number, type = ''): File {
  const file = new File([new Uint8Array(4)], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

beforeEach(() => {
  vi.stubGlobal(
    'AudioContext',
    vi.fn().mockImplementation(function AudioContextMock() {
      return {
        decodeAudioData: vi.fn().mockResolvedValue({
          getChannelData: () => Float32Array.from([0.1, 0.2, 0.3]),
        }),
        close: vi.fn().mockResolvedValue(undefined),
      }
    }),
  )
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FilePreview', () => {
  it('shows the file name and a human-readable size', () => {
    render(
      <FilePreview file={makeFile('mix.mp3', 5_242_880)} onRemove={vi.fn()} />,
    )

    expect(screen.getByText('mix.mp3')).toBeInTheDocument()
    expect(screen.getByText(/5 MB/i)).toBeInTheDocument()
  })

  it('uses the shared raised-surface treatment, since it sits on top of the dropzone', () => {
    const { container } = render(
      <FilePreview file={makeFile('mix.mp3', 1024)} onRemove={vi.fn()} />,
    )

    expect(container.firstChild).toHaveClass(...SURFACE_RAISED.split(' '))
  })

  it('calls onRemove when the remove button is clicked', async () => {
    const onRemove = vi.fn()
    const user = userEvent.setup()
    render(<FilePreview file={makeFile('mix.mp3', 1024)} onRemove={onRemove} />)

    await user.click(screen.getByRole('button', { name: /remove/i }))

    expect(onRemove).toHaveBeenCalledOnce()
  })

  it('renders an image thumbnail for image files', () => {
    render(
      <FilePreview
        file={makeFile('cover.png', 1024, 'image/png')}
        onRemove={vi.fn()}
      />,
    )

    expect(screen.getByRole('img', { name: /cover.png/i })).toBeInTheDocument()
  })

  it('renders a waveform preview for audio files', async () => {
    render(
      <FilePreview
        file={makeFile('mix.mp3', 1024, 'audio/mpeg')}
        onRemove={vi.fn()}
      />,
    )

    await waitFor(() =>
      expect(document.querySelector('svg')).toBeInTheDocument(),
    )
  })

  it('does not render a waveform preview for image files', () => {
    render(
      <FilePreview
        file={makeFile('cover.png', 1024, 'image/png')}
        onRemove={vi.fn()}
      />,
    )

    expect(document.querySelector('svg')).not.toBeInTheDocument()
  })
})
