import type { CSSProperties } from 'react'
import { Backdrop } from './Backdrop'
import { BrandMark } from './BrandMark'
import { BuildFooter } from './BuildFooter'
import { CopyJobLink } from './CopyJobLink'
import { JobVinylRecord } from './LiveVinylRecord'
import { OfflineBanner } from './OfflineBanner'
import { ProgressPanel } from './ProgressPanel'
import { SettingsRecap } from './SettingsRecap'

/** The screen for one job: its record, settings, progress or result, and its shareable link.
 * Rendered inside the form's provider, because the record reads the caption fields from it. */
export function JobScreen({
  jobId,
  artworkFile,
  moodStyle,
  onReset,
  onExpired,
}: {
  jobId: string
  artworkFile: File | null
  moodStyle: CSSProperties | undefined
  onReset: () => void
  onExpired: () => string | null
}) {
  return (
    <main
      className="relative min-h-screen overflow-clip bg-canvas text-ink"
      style={moodStyle}
    >
      <Backdrop />
      <OfflineBanner />
      <div className="relative mx-auto flex min-h-screen w-full max-w-4xl flex-col items-center justify-center gap-8 p-8 sm:flex-row sm:gap-12">
        <div className="w-full max-w-[16rem] shrink-0 [view-transition-name:vinyl] sm:max-w-[18rem]">
          <JobVinylRecord jobId={jobId} artworkFile={artworkFile} />
        </div>
        <div className="flex w-full max-w-sm flex-col items-center gap-6 text-center sm:items-start sm:text-left">
          <BrandMark className="h-8 w-8" />
          <h1 className="-mt-3 bg-gradient-to-b from-ink to-ink/60 bg-clip-text text-3xl font-semibold tracking-[-0.03em] text-transparent">
            DJ Visualizer Generator
          </h1>
          <SettingsRecap jobId={jobId} />
          <ProgressPanel
            jobId={jobId}
            onReset={onReset}
            posterFile={artworkFile}
            onExpired={onExpired}
          />
          <CopyJobLink jobId={jobId} />
        </div>
      </div>
      <div className="relative flex justify-center pb-6">
        <BuildFooter />
      </div>
    </main>
  )
}
