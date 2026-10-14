import { describe, expect, it } from 'vitest'
import { pollIntervalFor } from './useJobStatus'

describe('pollIntervalFor', () => {
  it('polls fast until there is something to compare', () => {
    expect(pollIntervalFor(undefined, { status: 'Queued', progress: 0 })).toBe(
      2000,
    )
  })

  it('slows down while nothing changes', () => {
    expect(
      pollIntervalFor(
        { status: 'Processing', progress: 40 },
        { status: 'Processing', progress: 40 },
      ),
    ).toBe(5000)
  })

  it('snaps back to fast on any progress or status change', () => {
    expect(
      pollIntervalFor(
        { status: 'Processing', progress: 40 },
        { status: 'Processing', progress: 41 },
      ),
    ).toBe(2000)
    expect(
      pollIntervalFor(
        { status: 'Queued', progress: 0 },
        { status: 'Processing', progress: 0 },
      ),
    ).toBe(2000)
  })
})
