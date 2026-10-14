import { useEffect } from 'react'
import type { JobStatusResponse } from '../api/client'
import { setFaviconProgress } from '../lib/faviconProgress'

const DEFAULT_TITLE = 'DJ Visualizer Generator'

/** Mirrors a job's status in the tab's title and favicon while its screen is open. */
export function useTabStatus(data: JobStatusResponse | undefined) {
  // Lets a backgrounded tab communicate status without being watched - the alternative is a
  // visitor tabbing away during a multi-minute render and having no way to tell it apart from a
  // frozen one without switching back.
  useEffect(() => {
    setFaviconProgress(data?.status === 'Processing' ? data.progress : null)
    // A titled job names itself in the tab - several renders in several tabs, or a shared
    // link, are otherwise indistinguishable.
    const base = data?.title
      ? `${data.title} · ${DEFAULT_TITLE}`
      : DEFAULT_TITLE
    if (data?.status === 'Processing') {
      document.title = `${data.progress}% · ${base}`
    } else if (data?.status === 'Queued') {
      document.title = `Queued · ${base}`
    } else {
      // Completed, Failed, or no data yet - none of these should leave the last render
      // percentage sitting in the tab title after the job has moved past rendering.
      document.title = base
    }
  }, [data?.status, data?.progress, data?.title])

  useEffect(() => {
    return () => {
      document.title = DEFAULT_TITLE
      setFaviconProgress(null)
    }
  }, [])
}
