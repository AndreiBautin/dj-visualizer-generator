import { useState } from 'react'
import { flushSync } from 'react-dom'
import { jobIdFromSearch, withJobId } from '../lib/jobUrl'

/** The open job, mirrored in the address bar's `?job=` so a reload or a shared link returns to it. */
export function useJobIdRoute(): [
  string | null,
  (next: string | null) => void,
] {
  const [jobId, setJobIdState] = useState<string | null>(() =>
    jobIdFromSearch(window.location.search),
  )
  function setJobId(next: string | null) {
    window.history.replaceState(null, '', withJobId(window.location.href, next))
    // Form and job screens are different layouts; a view transition cross-fades them and
    // moves the record (view-transition-name: vinyl) from one position to the other instead
    // of the page jumping. Skipped where unsupported or when the visitor asked for less motion.
    const reduceMotion = window.matchMedia?.(
      '(prefers-reduced-motion: reduce)',
    ).matches
    if (
      typeof document.startViewTransition === 'function' &&
      !reduceMotion &&
      document.visibilityState === 'visible'
    ) {
      let applied = false
      const apply = () => {
        if (applied) return
        applied = true
        flushSync(() => setJobIdState(next))
      }
      const transition = document.startViewTransition(apply)
      // The screen change must never depend on the animation. A transition waits for a frame
      // to capture the old view, and a page that is not producing frames (a backgrounded or
      // occluded window when a long upload finishes) never gives it one - the URL had updated
      // while the form stayed on screen. If it has not run promptly, skip it and switch anyway.
      const fallback = window.setTimeout(() => {
        if (!applied) {
          transition.skipTransition()
          apply()
        }
      }, 400)
      const clear = () => window.clearTimeout(fallback)
      transition.updateCallbackDone.then(clear, clear)
      // A skipped or aborted animation rejects these; that is expected, not an error.
      transition.ready.catch(() => {})
    } else {
      setJobIdState(next)
    }
  }
  return [jobId, setJobId]
}
