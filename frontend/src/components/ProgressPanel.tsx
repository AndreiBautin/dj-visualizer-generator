import { useJobStatus } from '../hooks/useJobStatus'
import { DownloadPanel } from './DownloadPanel'

interface ProgressPanelProps {
  jobId: string
  onReset: () => void
}

const STATUS_LABEL: Record<string, string> = {
  Queued: 'Queued - waiting for a worker to pick this up',
  Processing: 'Rendering your video',
}

export function ProgressPanel({ jobId, onReset }: ProgressPanelProps) {
  const { data, isPending, isError } = useJobStatus(jobId)

  if (isPending) {
    return <p className="text-center text-white/60">Checking status...</p>
  }

  if (isError || !data) {
    return <p className="text-center text-red-400">Could not check the job status. Retrying...</p>
  }

  if (data.status === 'Completed') {
    return <DownloadPanel jobId={jobId} onReset={onReset} />
  }

  if (data.status === 'Failed') {
    return (
      <div className="flex flex-col items-center gap-4 text-center">
        <p className="text-lg font-semibold text-red-400">Rendering failed</p>
        <p className="text-sm text-white/70">{data.errorMessage}</p>
        <button type="button" onClick={onReset} className="text-sm text-white/60 hover:text-white">
          Try again
        </button>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center gap-3 text-center">
      <p className="text-white/80">{STATUS_LABEL[data.status] ?? data.status}</p>
      <div
        role="progressbar"
        aria-valuenow={data.progress}
        aria-valuemin={0}
        aria-valuemax={100}
        className="h-2 w-64 overflow-hidden rounded-full bg-white/10"
      >
        <div className="h-full bg-white transition-all" style={{ width: `${data.progress}%` }} />
      </div>
      <p className="text-sm text-white/60">{data.progress}%</p>
    </div>
  )
}
