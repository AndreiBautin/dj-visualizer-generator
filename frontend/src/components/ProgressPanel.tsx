import { useEffect } from 'react'
import { useJobStatus } from '../hooks/useJobStatus'
import { DownloadPanel } from './DownloadPanel'

interface ProgressPanelProps {
  jobId: string
  onReset: () => void
}

const STATUS_LABEL: Record<string, string> = {
  Queued: 'Queued',
  Processing: 'Rendering your video',
}

const DEFAULT_TITLE = 'DJ Visualizer Generator'

export function ProgressPanel({ jobId, onReset }: ProgressPanelProps) {
  const { data, isPending, isError } = useJobStatus(jobId)

  // Lets a backgrounded tab communicate status without being watched - the alternative is a
  // visitor tabbing away during a multi-minute render and having no way to tell it apart from a
  // frozen one without switching back.
  useEffect(() => {
    if (data?.status === 'Processing') {
      document.title = `${data.progress}% · ${DEFAULT_TITLE}`
    } else if (data?.status === 'Queued') {
      document.title = `Queued · ${DEFAULT_TITLE}`
    } else {
      // Completed, Failed, or no data yet - none of these should leave the last render
      // percentage sitting in the tab title after the job has moved past rendering.
      document.title = DEFAULT_TITLE
    }
  }, [data?.status, data?.progress])

  useEffect(() => {
    return () => {
      document.title = DEFAULT_TITLE
    }
  }, [])

  if (isPending) {
    return <p className="text-center text-white/60">Checking on your render...</p>
  }

  if (isError || !data) {
    return <p className="text-center text-red-400">Lost the connection for a moment - retrying...</p>
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

  const isIndeterminate = data.status === 'Queued'

  return (
    <div className="flex flex-col items-center gap-3 text-center">
      <p className="text-white/80">{STATUS_LABEL[data.status] ?? data.status}</p>
      <div
        role="progressbar"
        aria-valuenow={isIndeterminate ? undefined : data.progress}
        aria-valuemin={0}
        aria-valuemax={100}
        className="h-2 w-64 overflow-hidden rounded-full bg-white/10"
      >
        {isIndeterminate ? (
          <div className="h-full w-1/3 rounded-full bg-white/60 motion-safe:animate-[progress-sweep_1.4s_ease-in-out_infinite]" />
        ) : (
          <div className="h-full bg-white transition-all" style={{ width: `${data.progress}%` }} />
        )}
      </div>
      <p className="text-sm text-white/60">
        {isIndeterminate ? 'Waiting for a worker to pick this up...' : `${data.progress}%`}
      </p>
    </div>
  )
}
