import { useEffect, useState } from 'react'

interface VinylRecordProps {
  /** The artwork the visitor has selected, if any - shown as the record's center label. */
  artworkFile: File | null
  /** Seconds for one full rotation - the same number the render uses, so the record here and
   * the video it stands in for turn at the same speed. It was multiplied by six here and not
   * in the render, so a "3s per spin" setting spun for 18s on this page and 3s in the video. */
  rotationSpeedSeconds: number
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
}: VinylRecordProps) {
  const [labelUrl, setLabelUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!artworkFile) {
      setLabelUrl(null)
      return
    }
    const url = URL.createObjectURL(artworkFile)
    setLabelUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [artworkFile])

  return (
    <div
      aria-hidden="true"
      className="relative aspect-square w-full max-w-[22rem]"
    >
      <div
        className="absolute inset-0 rounded-full motion-safe:animate-[spin_var(--vinyl-duration)_linear_infinite]"
        style={
          {
            '--vinyl-duration': `${rotationSpeedSeconds}s`,
            background:
              'repeating-radial-gradient(circle at center, #0a0a0a 0, #0a0a0a 2px, #1c1c1c 3px, #0a0a0a 4px)',
            boxShadow:
              '0 25px 60px -15px rgba(0,0,0,0.7), inset 0 0 0 1px rgba(255,255,255,0.06)',
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
    </div>
  )
}
