import { describe, expect, it } from 'vitest'
import { estimateRemainingMs, formatClock } from './renderTiming'

describe('render timing', () => {
  it('extrapolates linearly from progress so far', () => {
    expect(estimateRemainingMs(30_000, 25)).toBe(90_000)
  })

  it('withholds a guess while progress is too small to mean anything', () => {
    expect(estimateRemainingMs(5_000, 9)).toBeNull()
  })

  it('formats a clock with padded seconds', () => {
    expect(formatClock(65_000)).toBe('1:05')
  })
})
