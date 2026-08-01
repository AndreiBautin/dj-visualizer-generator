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
      <button type="button" onClick={onReset} className="text-sm text-white/60 hover:text-white">
        Create another video
      </button>
    </div>
  )
}
