/**
 * The page atmosphere behind both screens: two slow-drifting accent glows and a fine grain.
 * The grain is what stops a dark gradient from banding into visible steps on 8-bit displays,
 * so it is part of the look rather than decoration.
 *
 * The glows drift only at lg and up. Measured at 6x CPU throttling the drift costs no main-
 * thread time (it is a compositor transform), but every glass card's backdrop-blur must
 * re-sample whatever moves beneath it each frame - GPU fill a CPU profile does not show and a
 * low-end phone feels first. On a phone the glows hold still and the cards blur once.
 */
export function Backdrop() {
  return (
    <div
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 overflow-hidden"
    >
      <div className="absolute -top-1/4 -left-1/4 h-[70vmax] w-[70vmax] rounded-full bg-[radial-gradient(circle,color-mix(in_oklch,var(--mood)_22%,transparent),transparent_60%)] motion-safe:lg:animate-[drift_24s_ease-in-out_infinite_alternate]" />
      <div className="absolute -right-1/4 -bottom-1/3 h-[60vmax] w-[60vmax] rounded-full bg-[radial-gradient(circle,color-mix(in_oklch,var(--color-accent-glow)_12%,transparent),transparent_60%)] motion-safe:lg:animate-[drift_30s_ease-in-out_infinite_alternate-reverse]" />
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,transparent_40%,var(--vignette))]" />
      <div className="absolute inset-0 bg-[url('/grain.svg')] opacity-[0.06] mix-blend-overlay" />
    </div>
  )
}
