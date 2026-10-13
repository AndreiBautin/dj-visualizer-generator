const PARAM = 'job'

/** The job a URL points at, if any. The server decides whether it exists; this only parses. */
export function jobIdFromSearch(search: string): string | null {
  const value = new URLSearchParams(search).get(PARAM)?.trim()
  return value ? value : null
}

/** The same URL with the job parameter set, or removed when `jobId` is null. */
export function withJobId(href: string, jobId: string | null): string {
  const url = new URL(href)
  if (jobId) url.searchParams.set(PARAM, jobId)
  else url.searchParams.delete(PARAM)
  return url.toString()
}
