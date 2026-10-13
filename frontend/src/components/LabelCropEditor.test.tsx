import { render, screen } from '@testing-library/react'
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
})
