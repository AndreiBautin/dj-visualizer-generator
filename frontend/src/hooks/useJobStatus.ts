import { useQuery } from '@tanstack/react-query'
import { getJobStatus, type JobStatusResponse } from '../api/client'

const POLL_INTERVAL_MS = 2000

export function useJobStatus(jobId: string) {
  return useQuery({
    queryKey: ['job-status', jobId],
    queryFn: () => getJobStatus(jobId),
    refetchInterval: (query) => (isTerminal(query.state.data) ? false : POLL_INTERVAL_MS),
    // React Query pauses refetchInterval while the document is hidden. That default suits most
    // apps, but a render here takes minutes and switching tabs while waiting is the expected
    // behaviour, not the exception - progress would freeze the moment the user looked away. The
    // cost is polling a small JSON document every two seconds, and it stops on its own the
    // instant the job reaches a terminal state.
    refetchIntervalInBackground: true,
  })
}

function isTerminal(data: JobStatusResponse | undefined): boolean {
  return data?.status === 'Completed' || data?.status === 'Failed'
}
