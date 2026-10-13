import { describe, expect, it } from 'vitest'
import { jobIdFromSearch, withJobId } from './jobUrl'

describe('job URL', () => {
  it('round-trips a job id through the address', () => {
    const href = withJobId('https://example.test/app/?x=1', 'abc-123')
    expect(jobIdFromSearch(new URL(href).search)).toBe('abc-123')
    expect(new URL(href).searchParams.get('x')).toBe('1')
  })

  it('clears the job so a reset does not reopen it on reload', () => {
    const href = withJobId('https://example.test/?job=abc-123', null)
    expect(jobIdFromSearch(new URL(href).search)).toBeNull()
  })

  it('treats an empty parameter as no job', () => {
    expect(jobIdFromSearch('?job=')).toBeNull()
  })
})
