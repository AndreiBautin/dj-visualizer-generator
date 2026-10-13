import { describe, expect, it } from 'vitest'
import { addRecentJob, parseRecentJobs, type RecentJob } from './recentJobs'

const job = (jobId: string, createdAt = 0): RecentJob => ({
  jobId,
  title: '',
  createdAt,
})

describe('recent jobs', () => {
  it('puts the newest first and moves a repeat to the front instead of duplicating it', () => {
    const list = addRecentJob(
      addRecentJob([job('a'), job('b')], job('c')),
      job('a'),
    )
    expect(list.map((j) => j.jobId)).toEqual(['a', 'c', 'b'])
  })

  it('keeps only the five most recent', () => {
    let list: RecentJob[] = []
    for (const id of ['1', '2', '3', '4', '5', '6'])
      list = addRecentJob(list, job(id))
    expect(list.map((j) => j.jobId)).toEqual(['6', '5', '4', '3', '2'])
  })

  it('treats corrupted storage as empty rather than throwing', () => {
    expect(parseRecentJobs('{not json')).toEqual([])
    expect(parseRecentJobs(JSON.stringify([{ jobId: 1 }, job('ok')]))).toEqual([
      job('ok'),
    ])
  })
})
