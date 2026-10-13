import { describe, expect, it } from 'vitest'
import { DEFAULT_LABEL_CROP, clampCrop, labelImageRect } from './labelCrop'

describe('labelImageRect', () => {
  it('centre-crops a landscape image by default, like the render', () => {
    // 2:1 image covering a unit square: 2 wide, 1 tall, centred.
    expect(labelImageRect(200, 100, DEFAULT_LABEL_CROP)).toEqual({
      width: 2,
      height: 1,
      left: -0.5,
      top: -0,
    })
  })

  it('slides to the far edge at focus 1 and enlarges with zoom', () => {
    const rect = labelImageRect(100, 100, { focusX: 1, focusY: 0, zoom: 2 })
    expect(rect).toEqual({ width: 2, height: 2, left: -1, top: -0 })
  })

  it('clamps out-of-range values into what the server accepts', () => {
    expect(clampCrop({ focusX: -1, focusY: 2, zoom: 9 })).toEqual({
      focusX: 0,
      focusY: 1,
      zoom: 4,
    })
  })
})
