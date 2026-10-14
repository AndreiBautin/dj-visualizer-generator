import { useState } from 'react'
import {
  addRecentJob,
  loadRecentJobs,
  saveRecentJobs,
  type RecentJob,
} from '../lib/recentJobs'

/**
 * The visitor's recent renders, kept in localStorage so a closed tab can find its job again.
 * `now` is the clock, injectable so recent-render times are testable.
 */
export function useRecentJobs(now: () => number) {
  const [recentJobs, setRecentJobs] = useState<RecentJob[]>(loadRecentJobs)
  function rememberJob(jobId: string, title: string) {
    const next = addRecentJob(recentJobs, { jobId, title, createdAt: now() })
    setRecentJobs(next)
    saveRecentJobs(next)
  }
  /** Drops a job the server no longer has, so the list does not keep offering dead links. */
  function forgetJob(jobId: string): string | null {
    if (!recentJobs.some((job) => job.jobId === jobId)) return null
    const next = recentJobs.filter((job) => job.jobId !== jobId)
    setRecentJobs(next)
    saveRecentJobs(next)
    return 'Removed it from your recent renders.'
  }
  return { recentJobs, rememberJob, forgetJob }
}
