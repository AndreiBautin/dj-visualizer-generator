import type { RecentJob } from '../lib/recentJobs'

/** Links back to renders started in this browser. Stored only here, never sent anywhere. */
export function RecentRenders({
  jobs,
  now,
  onOpen,
}: {
  jobs: RecentJob[]
  now: number
  onOpen: (jobId: string) => void
}) {
  if (jobs.length === 0) return null
  return (
    <section aria-label="Recent renders" className="border-t border-line pt-4">
      <p className="mb-2 text-xs font-medium tracking-[0.2em] text-ink-faint uppercase">
        Recent renders
      </p>
      <ul className="space-y-1">
        {jobs.map((job) => (
          <li key={job.jobId}>
            <button
              type="button"
              onClick={() => onOpen(job.jobId)}
              className="flex w-full items-center justify-between gap-3 rounded-lg px-2 py-1.5 text-left text-sm text-ink-muted transition-colors hover:bg-tint/[0.05] hover:text-ink focus-visible:ring-2 focus-visible:ring-accent/60 focus-visible:outline-none"
            >
              <span className="truncate">{job.title || 'Untitled mix'}</span>
              <span className="shrink-0 text-xs text-ink-faint tabular-nums">
                {formatAgo(now - job.createdAt)}
              </span>
            </button>
          </li>
        ))}
      </ul>
      <p className="mt-2 text-[11px] text-ink-faint">
        Kept in this browser only. Renders expire on the server after a while.
      </p>
    </section>
  )
}

function formatAgo(ms: number): string {
  const minutes = Math.floor(ms / 60_000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h ago`
  return `${Math.floor(hours / 24)} d ago`
}
