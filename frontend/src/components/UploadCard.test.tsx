import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { UploadCard } from './UploadCard'

function makeFile(name: string, type = ''): File {
  return new File([new Uint8Array(4)], name, { type })
}

describe('UploadCard', () => {
  it('shows the label and hint when no file is selected', () => {
    render(
      <UploadCard
        label="Audio file"
        hint="MP3, WAV, FLAC, or M4A"
        accept=".mp3,.wav,.flac,.m4a"
        file={null}
        onFileSelected={vi.fn()}
        onClear={vi.fn()}
      />,
    )

    expect(screen.getByText('Audio file')).toBeInTheDocument()
    expect(screen.getByText('MP3, WAV, FLAC, or M4A')).toBeInTheDocument()
  })

  it('calls onFileSelected when a file is chosen via the file input', async () => {
    const onFileSelected = vi.fn()
    const user = userEvent.setup()
    render(
      <UploadCard
        label="Audio file"
        hint="hint"
        accept=".mp3"
        file={null}
        onFileSelected={onFileSelected}
        onClear={vi.fn()}
      />,
    )

    const file = makeFile('mix.mp3')
    const input = screen.getByLabelText('Audio file', { selector: 'input' })
    await user.upload(input, file)

    expect(onFileSelected).toHaveBeenCalledWith(file)
  })

  it('calls onFileSelected when a file is dropped', () => {
    const onFileSelected = vi.fn()
    render(
      <UploadCard
        label="Audio file"
        hint="hint"
        accept=".mp3"
        file={null}
        onFileSelected={onFileSelected}
        onClear={vi.fn()}
      />,
    )

    const file = makeFile('mix.mp3')
    const dropzone = screen.getByTestId('upload-dropzone')
    fireEvent.drop(dropzone, { dataTransfer: { files: [file] } })

    expect(onFileSelected).toHaveBeenCalledWith(file)
  })

  it('shows the selected file instead of the prompt', () => {
    render(
      <UploadCard
        label="Audio file"
        hint="hint"
        accept=".mp3"
        file={makeFile('mix.mp3')}
        onFileSelected={vi.fn()}
        onClear={vi.fn()}
      />,
    )

    expect(screen.getByText('mix.mp3')).toBeInTheDocument()
    expect(screen.queryByText('hint')).not.toBeInTheDocument()
  })

  it('calls onClear when the selected file is removed', async () => {
    const onClear = vi.fn()
    const user = userEvent.setup()
    render(
      <UploadCard
        label="Audio file"
        hint="hint"
        accept=".mp3"
        file={makeFile('mix.mp3')}
        onFileSelected={vi.fn()}
        onClear={onClear}
      />,
    )

    await user.click(screen.getByRole('button', { name: /remove/i }))

    expect(onClear).toHaveBeenCalledOnce()
  })

  it('renders a validation error when provided', () => {
    render(
      <UploadCard
        label="Audio file"
        hint="hint"
        accept=".mp3"
        file={null}
        error="Unsupported audio format."
        onFileSelected={vi.fn()}
        onClear={vi.fn()}
      />,
    )

    expect(screen.getByText('Unsupported audio format.')).toBeInTheDocument()
  })
})
