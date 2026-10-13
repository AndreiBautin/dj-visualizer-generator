import { useObjectUrl } from '../hooks/useObjectUrl'

interface VinylRecordProps {
  /** The artwork the visitor has selected, if any - shown as the record's center label. */
  artworkFile: File | null
  /** Seconds for one full rotation - the same number the render uses, so the record here and
   * the video it stands in for turn at the same speed. It was multiplied by six here and not
   * in the render, so a "3s per spin" setting spun for 18s on this page and 3s in the video. */
  rotationSpeedSeconds: number
  /** `idle` turns at the chosen speed (the form), `rendering` turns faster while the job runs,
   * `stopped` comes to rest once there is nothing left to wait for. */
  motion?: 'idle' | 'rendering' | 'stopped'
}

/**
 * A slowly spinning record, rendered from CSS rather than an image - and the render worker draws
 * this exact record (VinylFilterGraphBuilder takes every proportion from here), so it is a true
 * preview: change one and change the other. Shows the visitor's own artwork as the label once
 * selected, so it previews their video rather than staying generic while they fill in the form.
 */
export function VinylRecord({
  artworkFile,
  rotationSpeedSeconds,
  motion = 'idle',
}: VinylRecordProps) {
  const labelUrl = useObjectUrl(artworkFile)

  return (
    <div
      aria-hidden="true"
      className="relative isolate aspect-square w-full max-w-[22rem]"
    >
      <div
        className="absolute inset-0 rounded-full motion-safe:animate-[spin_var(--vinyl-duration)_linear_infinite]"
        style={
          {
            '--vinyl-duration': `${
              motion === 'rendering'
                ? Math.max(1, rotationSpeedSeconds / 3)
                : rotationSpeedSeconds
            }s`,
            animationPlayState: motion === 'stopped' ? 'paused' : 'running',
            background:
              'repeating-radial-gradient(circle at center, #0a0a0a 0, #0a0a0a 2px, #1c1c1c 3px, #0a0a0a 4px)',
            boxShadow:
              '0 40px 80px -20px rgba(0,0,0,0.85), 0 0 0 1px rgba(255,255,255,0.05), inset 0 0 0 1px rgba(255,255,255,0.06)',
          } as React.CSSProperties
        }
      >
        <div className="absolute inset-[6%] rounded-full bg-[radial-gradient(circle_at_35%_30%,rgba(255,255,255,0.06),transparent_60%)]" />
        <div className="absolute inset-[30%] flex items-center justify-center overflow-hidden rounded-full bg-accent shadow-[inset_0_0_0_1px_rgba(0,0,0,0.3)]">
          {labelUrl ? (
            <img src={labelUrl} alt="" className="h-full w-full object-cover" />
          ) : (
            <span className="text-xs font-semibold uppercase tracking-widest text-accent-content/70">
              Your art here
            </span>
          )}
        </div>
        <div className="absolute inset-[47%] rounded-full bg-black shadow-[0_0_0_2px_rgba(255,255,255,0.15)]" />
      </div>
      {/*
        Page-only lighting, outside the spinning layer so it stays put while the grooves turn
        under it - which is what sells the record as a physical object. The render worker does
        not draw this; it is light falling on the preview, not part of the record's geometry.
      */}
      <div className="pointer-events-none absolute inset-0 rounded-full bg-[conic-gradient(from_200deg,transparent_0deg,rgb(255_255_255/0.07)_30deg,transparent_70deg,transparent_180deg,rgb(255_255_255/0.05)_210deg,transparent_250deg)] mix-blend-screen" />
      <div
        className={`pointer-events-none absolute -inset-[8%] -z-10 rounded-full transition-[opacity,scale] duration-1000 ${motion === 'stopped' ? 'opacity-100 scale-110' : 'opacity-70'} bg-[radial-gradient(circle,color-mix(in_oklch,var(--color-accent)_18%,transparent),transparent_65%)] blur-2xl`}
      />
    </div>
  )
}
