/** "1:05" style, for a running clock. */
export function formatClock(ms: number): string {
  const total = Math.max(0, Math.round(ms / 1000))
  const minutes = Math.floor(total / 60)
  const seconds = total % 60
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}

/**
 * A linear guess at time left from time spent so far. Withheld below 10%, where the first
 * seconds of ffmpeg start-up dominate and the guess would swing wildly.
 */
export function estimateRemainingMs(
  elapsedMs: number,
  progress: number,
): number | null {
  if (progress < 10 || progress >= 100 || elapsedMs <= 0) return null
  return (elapsedMs * (100 - progress)) / progress
}
