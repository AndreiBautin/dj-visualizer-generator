import { useQuery } from '@tanstack/react-query'
import { getJobStatus, type JobStatusResponse } from '../api/client'

const POLL_INTERVAL_MS = 2000

export function useJobStatus(jobId: string) {
  return useQuery({
    queryKey: ['job-status', jobId],
    queryFn: () => getJobStatus(jobId),
    refetchInterval: (query) => (isTerminal(query.state.data) ? false : POLL_INTERVAL_MS),
  })
}

function isTerminal(data: JobStatusResponse | undefined): boolean {
  return data?.status === 'Completed' || data?.status === 'Failed'
}
