import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DEFAULT_LABEL_CROP } from '../lib/labelCrop'
import { LabelCropEditor } from './LabelCropEditor'

describe('LabelCropEditor', () => {
  const file = new File(['x'], 'art.png', { type: 'image/png' })

  it('moves and zooms the crop from the keyboard, within the server bounds', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:art')
    URL.revokeObjectURL = vi.fn()
    const onChange = vi.fn()
    const user = userEvent.setup()
    render(
      <LabelCropEditor
        file={file}
        crop={DEFAULT_LABEL_CROP}
        onChange={onChange}
      />,
    )

    screen.getByRole('group', { name: /record label crop/i }).focus()
    await user.keyboard('{ArrowRight}')
    expect(onChange).toHaveBeenLastCalledWith({
      focusX: 0.45,
      focusY: 0.5,
      zoom: 1,
    })

    await user.keyboard('-')
    // Already at the minimum zoom - clamped, not sent below what the API accepts.
    expect(onChange).toHaveBeenLastCalledWith({
      focusX: 0.5,
      focusY: 0.5,
      zoom: 1,
    })
  })

  // Dragging moves the image with the pointer, so the crop window moves the other way. A flipped
  // sign would drag the picture away from the hand and nothing else would catch it: the visual
  // suite only warns.
  it('moves the crop opposite to the drag, scaled by how far the image overhangs', () => {
    URL.createObjectURL = vi.fn(() => 'blob:art')
    URL.revokeObjectURL = vi.fn()
    const onChange = vi.fn()
    render(
      <LabelCropEditor
        file={file}
        crop={DEFAULT_LABEL_CROP}
        onChange={onChange}
      />,
    )
    const frame = screen.getByRole('group', { name: /record label crop/i })
    const image = frame.querySelector('img')!
    // A 200px frame showing a 400x200 image: 200px of horizontal travel, none vertically.
    Object.defineProperty(frame, 'clientWidth', { value: 200 })
    Object.defineProperty(image, 'clientWidth', { value: 400 })
    Object.defineProperty(image, 'clientHeight', { value: 200 })
    frame.setPointerCapture = vi.fn()

    fireEvent.pointerDown(frame, { pointerId: 1, clientX: 100, clientY: 100 })
    fireEvent.pointerMove(frame, { pointerId: 1, clientX: 150, clientY: 100 })

    expect(onChange).toHaveBeenLastCalledWith({
      focusX: 0.25,
      focusY: 0.5,
      zoom: 1,
    })
  })
})
