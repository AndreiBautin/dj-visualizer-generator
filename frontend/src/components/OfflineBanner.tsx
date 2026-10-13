import { useOnline } from '../hooks/useOnline'

/** The installed app opens offline from its cache; rendering still needs the server. */
export function OfflineBanner() {
  const online = useOnline()
  if (online) return null
  return (
    <div
      role="status"
      className="fixed inset-x-3 top-3 z-40 mx-auto max-w-md rounded-xl border border-line-strong bg-canvas/90 px-4 py-3 text-center text-sm text-ink shadow-lg backdrop-blur-md"
    >
      You’re offline. You can set things up, but uploading and rendering need a
      connection.
    </div>
  )
}
