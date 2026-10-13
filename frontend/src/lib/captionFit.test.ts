import { describe, expect, it } from 'vitest'
import { captionFits, captionWidthShare } from './captionFit'

// A stand-in face where every character is 60px wide at 100px.
const monospace60 = (text: string) => text.length * 60

describe('caption fit', () => {
  it('fits a typical title', () => {
    expect(
      captionFits(captionWidthShare('Friday Night Set', monospace60)),
    ).toBe(true)
  })

  it('flags a title the render would run off the frame', () => {
    // 70 characters * 0.6em * 0.044 / (16/9) ~ 1.04 of the width.
    expect(captionFits(captionWidthShare('x'.repeat(70), monospace60))).toBe(
      false,
    )
  })
})
