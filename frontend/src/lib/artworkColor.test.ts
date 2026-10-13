import { describe, expect, it } from 'vitest'
import { pickVividColor } from './artworkColor'

function pixels(...colors: [number, number, number, number][]) {
  return new Uint8ClampedArray(colors.flat())
}

describe('pickVividColor', () => {
  it('prefers a smaller vivid area over a large flat grey one', () => {
    const grey: [number, number, number, number] = [128, 128, 128, 255]
    const pink: [number, number, number, number] = [240, 40, 140, 255]
    expect(pickVividColor(pixels(grey, grey, grey, grey, pink))).toBe(
      'rgb(240 40 140)',
    )
  })

  it('returns null for greyscale artwork rather than a muddy glow', () => {
    expect(
      pickVividColor(pixels([20, 20, 20, 255], [200, 200, 200, 255])),
    ).toBeNull()
  })

  it('ignores transparent pixels', () => {
    expect(pickVividColor(pixels([255, 0, 0, 0]))).toBeNull()
  })
})
