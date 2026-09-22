import { useRegisterSW } from 'virtual:pwa-register/react'

/**
 * A render is tracked only in page state, so reloading mid-job silently strands the user's view
 * of it - the update must be offered, never applied out from under them.
 */
export function UpdatePrompt() {
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW()

  if (!needRefresh) {
    return null
  }

  return (
    <div className="fixed inset-x-0 bottom-0 z-50 flex justify-center p-4">
      <div className="flex items-center gap-3 rounded-lg border border-white/15 bg-neutral-900 px-4 py-3 text-sm text-white shadow-lg">
        <span>An update is ready.</span>
        <button
          type="button"
          onClick={() => updateServiceWorker(true)}
          className="rounded bg-white px-3 py-1 font-semibold text-black hover:bg-white/90"
        >
          Reload
        </button>
        <button
          type="button"
          onClick={() => setNeedRefresh(false)}
          className="text-white/50 hover:text-white/80"
        >
          Later
        </button>
      </div>
    </div>
  )
}
