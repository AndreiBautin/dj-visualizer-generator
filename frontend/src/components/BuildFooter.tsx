import { useQuery } from '@tanstack/react-query'
import { buildSha, getVersion } from '../api/client'

/**
 * Ties the running page to the commit it was built from.
 *
 * Prefers the commit the server reports, because that works on any host without threading a
 * build argument through the image - the build-time value is only present when someone passed
 * VITE_BUILD_SHA, and silently stays "unknown" otherwise. Renders nothing when neither is
 * available, which is the normal case for a local dev build.
 */
export function BuildFooter() {
  const { data } = useQuery({
    queryKey: ['version'],
    queryFn: getVersion,
    staleTime: Infinity,
    retry: false,
  })

  const commit = data?.commit ?? buildSha()
  if (!commit) {
    return null
  }

  return (
    <footer className="text-xs text-white/30">
      build <code>{commit}</code>
    </footer>
  )
}
