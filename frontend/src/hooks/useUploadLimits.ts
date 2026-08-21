import { useQuery } from '@tanstack/react-query'
import { getLimits, type UploadLimits } from '../api/client'
import { DEFAULT_UPLOAD_LIMITS } from '../lib/fileValidation'

/**
 * The server's limits, falling back to the self-hosted defaults until they arrive (or if the
 * request fails). The fallback only affects the client-side pre-check and the hint text - the
 * server enforces the real limits regardless of what this returns.
 */
export function useUploadLimits(): UploadLimits {
  const { data } = useQuery({
    queryKey: ['upload-limits'],
    queryFn: getLimits,
    staleTime: Infinity,
    retry: false,
  })

  return data ?? DEFAULT_UPLOAD_LIMITS
}
