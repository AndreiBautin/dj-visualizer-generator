import { ApiError } from '../api/client'
import { useEffect } from 'react'
import { useJobStatus } from '../hooks/useJobStatus'
import { BUTTON_SECONDARY } from '../lib/buttons'
import { DownloadPanel } from './DownloadPanel'

const STAGES = ['In queue', 'Rendering', 'Ready'] as const

/** Where the job is in its short life, so a long render reads as a journey with an end. */
function StageTrack({ current }: { current: number }) {
  return (
    <ol className="flex items-center gap-2 text-xs" aria-hidden="true">
      {STAGES.map((stage, index) => (
        <li key={stage} className="flex items-center gap-2">
          {index > 0 && (
            <span
              className={`h-px w-6 ${index <= current ? 'bg-accent/60' : 'bg-line-strong'}`}
            />
          )}
          <span
            className={`flex items-center gap-1.5 ${
              index === current
                ? 'font-medium text-ink'
                : index < current
                  ? 'text-ink-muted'
                  : 'text-ink-faint'
            }`}
          >
            <span
              className={`h-1.5 w-1.5 rounded-full ${
                index === current
                  ? 'bg-accent-glow shadow-[0_0_8px_var(--color-accent-glow)] motion-safe:animate-pulse'
                  : index < current
                    ? 'bg-accent'
                    : 'bg-line-strong'
              }`}
            />
            {stage}
          </span>
        </li>
      ))}
    </ol>
  )
}

/** One shape for every failure: what happened, and the single way forward. */
function ErrorState({
  title,
  detail,
  action,
  onAction,
}: {
  title: string
  detail?: string | null
  action: string
  onAction: () => void
}) {
  return (
    <div className="flex w-full flex-col items-center gap-4 rounded-2xl border border-danger/25 bg-danger/[0.06] p-6 text-center sm:items-start sm:text-left motion-safe:animate-[panel-in_220ms_ease-out]">
      <div className="flex items-start gap-3">
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="mt-0.5 h-5 w-5 shrink-0 text-danger"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
        >
          <circle cx="10" cy="10" r="7.5" />
          <path d="M10 6.5v4M10 13.5v.01" />
        </svg>
        <div>
          <p role="alert" className="font-semibold text-ink">
            {title}
          </p>
          {detail && <p className="mt-1 text-sm text-ink-muted">{detail}</p>}
        </div>
      </div>
      <button type="button" onClick={onAction} className={BUTTON_SECONDARY}>
        {action}
      </button>
    </div>
  )
}

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
  const { data, isPending, isError, error } = useJobStatus(jobId)

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
    return (
      <p className="text-ink-muted motion-safe:animate-pulse">
        Checking on your render...
      </p>
    )
  }

  if (error instanceof ApiError && error.status === 404) {
    return (
      <ErrorState
        title="This render has expired or the server restarted. Please create another video."
        action="Create another video"
        onAction={onReset}
      />
    )
  }

  if (isError || !data) {
    return (
      <p className="text-center text-danger">
        Lost the connection for a moment - retrying...
      </p>
    )
  }

  if (data.status === 'Completed') {
    return <DownloadPanel jobId={jobId} onReset={onReset} />
  }

  if (data.status === 'Failed') {
    return (
      <ErrorState
        title="Rendering failed"
        detail={data.errorMessage}
        action="Try again"
        onAction={onReset}
      />
    )
  }

  const isIndeterminate = data.status === 'Queued'

  return (
    <div className="flex w-full flex-col items-center gap-5 sm:items-start">
      <StageTrack current={isIndeterminate ? 0 : 1} />
      <div className="flex w-full items-end justify-between gap-4">
        <p className="text-ink-muted">
          {STATUS_LABEL[data.status] ?? data.status}
        </p>
        {!isIndeterminate && (
          <p className="text-4xl font-semibold tracking-tight text-ink tabular-nums">
            {`${data.progress}%`}
          </p>
        )}
      </div>
      <div
        role="progressbar"
        aria-valuenow={isIndeterminate ? undefined : data.progress}
        aria-valuemin={0}
        aria-valuemax={100}
        className="relative h-2 w-full overflow-hidden rounded-full bg-white/[0.08] shadow-[inset_0_1px_2px_rgb(0_0_0/0.5)]"
      >
        {isIndeterminate ? (
          <div className="h-full w-1/3 rounded-full bg-accent/70 motion-safe:animate-[progress-sweep_1.4s_ease-in-out_infinite]" />
        ) : (
          <div
            className="h-full rounded-full bg-gradient-to-r from-accent to-accent-glow shadow-[0_0_16px_var(--color-accent)] transition-[width] duration-700 ease-out"
            style={{ width: `${data.progress}%` }}
          />
        )}
      </div>
      {isIndeterminate && (
        <p className="text-sm text-ink-faint">
          Waiting for a worker to pick this up...
        </p>
      )}
    </div>
  )
}
