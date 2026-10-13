export interface RecentJob {
  jobId: string
  title: string
  createdAt: number
}

const KEY = 'spinner.recentJobs'
const LIMIT = 5

/** Newest first, de-duplicated by id, capped. Pure, so the ordering rules are testable. */
export function addRecentJob(list: RecentJob[], entry: RecentJob): RecentJob[] {
  return [entry, ...list.filter((job) => job.jobId !== entry.jobId)].slice(
    0,
    LIMIT,
  )
}

/**
 * Parses whatever is in storage without trusting it: a corrupted or hand-edited value
 * degrades to an empty list rather than breaking the page.
 */
export function parseRecentJobs(raw: unknown): RecentJob[] {
  if (typeof raw !== 'string') return []
  let value: unknown
  try {
    value = JSON.parse(raw)
  } catch {
    return []
  }
  if (!Array.isArray(value)) return []
  return value
    .filter(
      (item): item is RecentJob =>
        typeof item === 'object' &&
        item !== null &&
        typeof (item as RecentJob).jobId === 'string' &&
        typeof (item as RecentJob).title === 'string' &&
        typeof (item as RecentJob).createdAt === 'number',
    )
    .slice(0, LIMIT)
}

// Browser storage can be unavailable (private mode, blocked site data); every access is guarded
// and a failure just means no history, never a broken page.
export function loadRecentJobs(): RecentJob[] {
  try {
    return parseRecentJobs(window.localStorage.getItem(KEY))
  } catch {
    return []
  }
}

export function saveRecentJobs(list: RecentJob[]): void {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(list))
  } catch {
    // No history this session - nothing else depends on it.
  }
}
