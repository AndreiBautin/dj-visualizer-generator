import { downloadUrl } from '../api/client'

interface DownloadPanelProps {
  jobId: string
  onReset: () => void
}

export function DownloadPanel({ jobId, onReset }: DownloadPanelProps) {
  return (
    <div className="flex flex-col items-center gap-4 text-center">
      <p className="text-lg font-semibold text-white">Your video is ready.</p>
      <a
        href={downloadUrl(jobId)}
        download
        className="rounded-lg bg-white px-6 py-3 text-sm font-semibold text-black hover:bg-white/90"
      >
        Download MP4
      </a>
      {/*
        Stated up front because the alternative is a visitor meeting the limit as a raw 429 from a
        plain <a download>, which navigates away from the app to a JSON error document. The link
        stays a plain anchor deliberately - fetching the video into a blob to catch that response
        would pull a multi-hundred-megabyte file through memory to improve an error message.
      */}
      <p className="text-xs text-white/50">
        This link works for a few downloads and is deleted a short while after rendering.
      </p>
      <button type="button" onClick={onReset} className="text-sm text-white/60 hover:text-white">
        Create another video
      </button>
    </div>
  )
}
