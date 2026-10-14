import { useQuery } from '@tanstack/react-query'
import { useRef } from 'react'
import { ApiError, getJobStatus, type JobStatusResponse } from '../api/client'

const POLL_INTERVAL_MS = 2000
const SLOW_POLL_INTERVAL_MS = 5000

type Snapshot = Pick<JobStatusResponse, 'status' | 'progress'>

/**
 * Poll quickly while something is changing and slow down while it is not. A long render can sit
 * on one percent for a while (and queued jobs do not move at all), so the fixed 2 s cadence spent
 * most of its requests confirming nothing had happened. Any change snaps back to the fast rate.
 */
export function pollIntervalFor(
  previous: Snapshot | undefined,
  current: Snapshot | undefined,
): number {
  if (previous === undefined || current === undefined) return POLL_INTERVAL_MS
  return previous.status === current.status &&
    previous.progress === current.progress
    ? SLOW_POLL_INTERVAL_MS
    : POLL_INTERVAL_MS
}

export function useJobStatus(jobId: string) {
  const previous = useRef<
    { snapshot: Snapshot; updatedAt: number } | undefined
  >(undefined)
  const interval = useRef(POLL_INTERVAL_MS)

  return useQuery({
    queryKey: ['job-status', jobId],
    queryFn: () => getJobStatus(jobId),
    refetchInterval: (query) => {
      const { data, error, dataUpdatedAt } = query.state
      if (isTerminal(data) || isExpired(error)) return false
      // refetchInterval is re-read on every state change, not only after a fetch, so the
      // comparison advances only when a new response has arrived.
      if (data !== undefined && previous.current?.updatedAt !== dataUpdatedAt) {
        interval.current = pollIntervalFor(previous.current?.snapshot, data)
        previous.current = {
          snapshot: { status: data.status, progress: data.progress },
          updatedAt: dataUpdatedAt,
        }
      }
      return interval.current
    },
    // React Query pauses refetchInterval while the document is hidden. That default suits most
    // apps, but a render here takes minutes and switching tabs while waiting is the expected
    // behaviour, not the exception - progress would freeze the moment the user looked away. The
    // cost is polling a small JSON document every few seconds, and it stops on its own the
    // instant the job reaches a terminal state.
    refetchIntervalInBackground: true,
    retry: false, // The polling interval retries transient failures; 404 stops polling.
  })
}

function isTerminal(data: JobStatusResponse | undefined): boolean {
  return data?.status === 'Completed' || data?.status === 'Failed'
}

function isExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404
}
