import { useEffect, useRef, useState } from 'react'
import { useObjectUrl } from '../hooks/useObjectUrl'

const SPIN_DOWN_MS = 1400

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
  /** Plays a single ring burst behind the record - the video is ready. */
  celebrate?: boolean
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
  celebrate = false,
}: VinylRecordProps) {
  const labelUrl = useObjectUrl(artworkFile)
  const discRef = useRef<HTMLDivElement>(null)
  const [atRest, setAtRest] = useState(false)

  // A turntable coasts to a stop. CSS can only pause on the spot, so this ramps the running
  // animation's playback rate down over SPIN_DOWN_MS and then pauses it where it landed.
  // Without the Web Animations API (jsdom, very old browsers) it just stops.
  useEffect(() => {
    if (motion !== 'stopped') {
      setAtRest(false)
      return
    }
    const spin = discRef.current?.getAnimations?.()[0]
    if (!spin || typeof spin.updatePlaybackRate !== 'function') {
      setAtRest(true)
      return
    }
    let frame = 0
    const start = performance.now()
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / SPIN_DOWN_MS)
      spin.updatePlaybackRate(Math.max(0.0001, (1 - t) ** 2))
      if (t < 1) frame = requestAnimationFrame(step)
      else setAtRest(true)
    }
    frame = requestAnimationFrame(step)
    // Animation frames do not run in a hidden tab, and a render often finishes while the
    // visitor is elsewhere - so a timer guarantees the record is at rest when they return.
    const settle = setTimeout(() => setAtRest(true), SPIN_DOWN_MS + 100)
    return () => {
      cancelAnimationFrame(frame)
      clearTimeout(settle)
      spin.updatePlaybackRate(1)
    }
  }, [motion])

  return (
    <div
      aria-hidden="true"
      className="relative isolate aspect-square w-full max-w-[22rem]"
    >
      <div
        ref={discRef}
        className="absolute inset-0 rounded-full motion-safe:animate-[spin_var(--vinyl-duration)_linear_infinite]"
        style={
          {
            '--vinyl-duration': `${
              motion === 'rendering'
                ? Math.max(1, rotationSpeedSeconds / 3)
                : rotationSpeedSeconds
            }s`,
            animationPlayState: atRest ? 'paused' : 'running',
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
        className={`pointer-events-none absolute -inset-[8%] -z-10 rounded-full transition-[opacity,scale] duration-1000 ${motion === 'stopped' ? 'opacity-100 scale-110' : 'opacity-70'} bg-[radial-gradient(circle,color-mix(in_oklch,var(--mood)_18%,transparent),transparent_65%)] blur-2xl`}
      />
      {celebrate && (
        <>
          <div className="pointer-events-none absolute inset-0 -z-10 rounded-full border-2 border-[var(--mood)] opacity-0 motion-safe:animate-[burst_1100ms_cubic-bezier(.2,.7,.3,1)_both]" />
          <div className="pointer-events-none absolute inset-0 -z-10 rounded-full border border-[var(--mood)] opacity-0 motion-safe:animate-[burst_1300ms_cubic-bezier(.2,.7,.3,1)_180ms_both]" />
        </>
      )}
    </div>
  )
}
