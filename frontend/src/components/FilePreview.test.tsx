import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { FilePreview } from './FilePreview'

function makeFile(name: string, size: number, type = ''): File {
  const file = new File([new Uint8Array(4)], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

describe('FilePreview', () => {
  it('shows the file name and a human-readable size', () => {
    render(<FilePreview file={makeFile('mix.mp3', 5_242_880)} onRemove={vi.fn()} />)

    expect(screen.getByText('mix.mp3')).toBeInTheDocument()
    expect(screen.getByText(/5 MB/i)).toBeInTheDocument()
  })

  it('calls onRemove when the remove button is clicked', async () => {
    const onRemove = vi.fn()
    const user = userEvent.setup()
    render(<FilePreview file={makeFile('mix.mp3', 1024)} onRemove={onRemove} />)

    await user.click(screen.getByRole('button', { name: /remove/i }))

    expect(onRemove).toHaveBeenCalledOnce()
  })

  it('renders an image thumbnail for image files', () => {
    render(<FilePreview file={makeFile('cover.png', 1024, 'image/png')} onRemove={vi.fn()} />)

    expect(screen.getByRole('img', { name: /cover.png/i })).toBeInTheDocument()
  })
})
