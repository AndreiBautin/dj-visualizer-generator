import { downloadUrl, previewUrl } from '../api/client'
import { BUTTON_GHOST, BUTTON_PRIMARY } from '../lib/buttons'

interface DownloadPanelProps {
  jobId: string
  onReset: () => void
}

export function DownloadPanel({ jobId, onReset }: DownloadPanelProps) {
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
        className="aspect-video w-full rounded-2xl bg-black shadow-[0_30px_60px_-20px_rgb(0_0_0/0.8)] ring-1 ring-line-strong"
      />
      <a
        href={downloadUrl(jobId)}
        download
        className={`w-full sm:w-auto ${BUTTON_PRIMARY}`}
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
      {/*
        Stated up front because the alternative is a visitor meeting the limit as a raw 429 from a
        plain <a download>, which navigates away from the app to a JSON error document. The link
        stays a plain anchor deliberately - fetching the video into a blob to catch that response
        would pull a multi-hundred-megabyte file through memory to improve an error message.
      */}
      <p className="max-w-sm text-xs leading-relaxed text-ink-faint">
        You can save this file five times. Previewing does not use those saves.
        Files expire automatically.
      </p>
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
