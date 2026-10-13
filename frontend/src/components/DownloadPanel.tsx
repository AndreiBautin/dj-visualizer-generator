import { useEffect, useState } from 'react'
import { downloadUrl, previewUrl } from '../api/client'
import { useObjectUrl } from '../hooks/useObjectUrl'
import { BUTTON_GHOST, BUTTON_PRIMARY } from '../lib/buttons'

interface DownloadPanelProps {
  jobId: string
  downloadsRemaining: number
  /** The visitor's own artwork, shown before playback; absent for the bundled sample. */
  posterFile?: File | null
  onDownloadStarted: () => void
  onReset: () => void
}

export function DownloadPanel({
  jobId,
  downloadsRemaining,
  onDownloadStarted,
  onReset,
  posterFile,
}: DownloadPanelProps) {
  const posterUrl = useObjectUrl(posterFile)
  const exhausted = downloadsRemaining <= 0
  const [justStarted, setJustStarted] = useState(false)

  // A download gives no in-page sign it began - the browser's tray is easy to miss - so the
  // page says so for a few seconds while the saves count refreshes.
  useEffect(() => {
    if (!justStarted) return
    const timer = setTimeout(() => setJustStarted(false), 4000)
    return () => clearTimeout(timer)
  }, [justStarted])
  return (
    <div className="flex w-full flex-col items-center gap-5 text-center sm:items-start sm:text-left motion-safe:animate-[panel-in_220ms_ease-out]">
      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 items-center justify-center rounded-full bg-accent/15 text-accent-glow ring-1 ring-accent/40"
        >
          <svg
            viewBox="0 0 20 20"
            className="h-4 w-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M5 10.5l3.2 3L15 7" />
          </svg>
        </span>
        <p className="text-lg font-semibold text-ink">Your video is ready.</p>
      </div>
      <video
        controls
        preload="none"
        src={previewUrl(jobId)}
        poster={posterUrl ?? undefined}
        className="aspect-video w-full rounded-2xl bg-black shadow-[0_30px_60px_-20px_rgb(0_0_0/0.8)] ring-1 ring-line-strong"
      />
      <a
        href={exhausted ? undefined : downloadUrl(jobId)}
        download
        aria-disabled={exhausted}
        onClick={
          exhausted
            ? undefined
            : () => {
                setJustStarted(true)
                onDownloadStarted()
              }
        }
        className={`w-full sm:w-auto ${BUTTON_PRIMARY} ${exhausted ? 'pointer-events-none opacity-40' : ''}`}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="h-4 w-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M10 4v9M6.5 9.5L10 13l3.5-3.5M4 16h12" />
        </svg>
        Download MP4
      </a>
      <p
        aria-live="polite"
        className={`-mt-2 text-xs text-accent-glow transition-opacity duration-300 ${justStarted ? 'opacity-100' : 'opacity-0'}`}
      >
        {justStarted ? 'Download started - check your downloads.' : ''}
      </p>
      {/*
        Stated up front because the alternative is a visitor meeting the limit as a raw 429 from a
        plain <a download>, which navigates away from the app to a JSON error document. The link
        stays a plain anchor deliberately - fetching the video into a blob to catch that response
        would pull a multi-hundred-megabyte file through memory to improve an error message.
      */}
      <div className="flex flex-wrap items-center justify-center gap-2 text-xs text-ink-faint sm:justify-start">
        <span
          className={`rounded-full px-2.5 py-1 font-medium tabular-nums ring-1 ${
            exhausted
              ? 'bg-danger/10 text-danger ring-danger/30'
              : 'bg-white/[0.05] text-ink-muted ring-line-strong'
          }`}
        >
          {exhausted
            ? 'No saves left'
            : `${downloadsRemaining} ${downloadsRemaining === 1 ? 'save' : 'saves'} left`}
        </span>
        <span>Previewing is free. Files expire automatically.</span>
      </div>
      <button
        type="button"
        onClick={onReset}
        className={`sm:-ml-3 ${BUTTON_GHOST}`}
      >
        Create another video
      </button>
    </div>
  )
}
