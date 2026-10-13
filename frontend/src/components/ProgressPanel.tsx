import { ApiError } from '../api/client'
import { useEffect, useRef, useState } from 'react'
import { estimateRemainingMs, formatClock } from '../lib/renderTiming'
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

function TimingLine({
  elapsedMs,
  progress,
}: {
  elapsedMs: number
  progress: number
}) {
  const remaining = estimateRemainingMs(elapsedMs, progress)
  return (
    <p className="text-xs text-ink-faint tabular-nums">
      {formatClock(elapsedMs)} elapsed
      {remaining !== null && ` · about ${formatClock(remaining)} left`}
    </p>
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
  posterFile?: File | null
  /** The clock, injectable so elapsed time is testable. */
  now?: () => number
}

const STATUS_LABEL: Record<string, string> = {
  Queued: 'Queued',
  Processing: 'Rendering your video',
}

const DEFAULT_TITLE = 'DJ Visualizer Generator'

export function ProgressPanel({
  jobId,
  onReset,
  posterFile,
  now = Date.now,
}: ProgressPanelProps) {
  const { data, isPending, isError, error, refetch } = useJobStatus(jobId)

  // Measured from when this page first saw the job rendering - the server sends no start
  // time, and a page reopened mid-render honestly does not know more than that.
  const renderStartedAt = useRef<number | null>(null)
  const [clock, setClock] = useState(() => now())
  const isRendering = data?.status === 'Processing'
  if (isRendering && renderStartedAt.current === null) {
    renderStartedAt.current = now()
  }
  useEffect(() => {
    if (!isRendering) return
    const timer = setInterval(() => setClock(now()), 1000)
    return () => clearInterval(timer)
  }, [isRendering, now])

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
      // Same shape as the progress view it is about to become, so nothing jumps when the
      // first status lands - most visible when a shared link or a reload opens this screen.
      <div className="flex w-full flex-col items-center gap-5 sm:items-start">
        <p className="sr-only">Checking on your render...</p>
        <div
          aria-hidden="true"
          className="h-4 w-48 rounded-full bg-tint/[0.07] motion-safe:animate-pulse"
        />
        <div
          aria-hidden="true"
          className="flex w-full items-end justify-between"
        >
          <div className="h-4 w-32 rounded-full bg-tint/[0.07] motion-safe:animate-pulse" />
          <div className="h-9 w-20 rounded-lg bg-tint/[0.07] motion-safe:animate-pulse" />
        </div>
        <div
          aria-hidden="true"
          className="h-2 w-full rounded-full bg-tint/[0.07] motion-safe:animate-pulse"
        />
      </div>
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
    return (
      <DownloadPanel
        jobId={jobId}
        downloadsRemaining={data.downloadsRemaining}
        // Polling stops at Completed, so re-read once after a save starts - the server counts
        // the save when the transfer begins, a moment after the click.
        onDownloadStarted={() => setTimeout(() => void refetch(), 1500)}
        onReset={onReset}
        posterFile={posterFile}
      />
    )
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
        className="relative h-2 w-full overflow-hidden rounded-full bg-tint/[0.08] shadow-[inset_0_1px_2px_rgb(0_0_0/0.5)]"
      >
        {isIndeterminate ? (
          <div className="h-full w-1/3 rounded-full bg-accent/70 motion-safe:animate-[progress-sweep_1.4s_ease-in-out_infinite]" />
        ) : (
          <div
            className="h-full rounded-full bg-gradient-to-r from-[var(--mood)] to-[color-mix(in_oklch,var(--mood),white_25%)] shadow-[0_0_16px_var(--mood)] transition-colors transition-[width] duration-700 ease-out"
            style={{ width: `${data.progress}%` }}
          />
        )}
      </div>
      {isIndeterminate ? (
        <p className="text-sm text-ink-faint">
          Waiting for a worker to pick this up...
        </p>
      ) : (
        renderStartedAt.current !== null && (
          <TimingLine
            elapsedMs={Math.max(0, clock - renderStartedAt.current)}
            progress={data.progress}
          />
        )
      )}
    </div>
  )
}
