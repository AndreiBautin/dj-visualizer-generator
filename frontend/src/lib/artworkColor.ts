/**
 * The most vivid colour in an image's pixels, as an `rgb()` string - or null for artwork that
 * is essentially greyscale, where a "dominant colour" would just be a muddy grey glow.
 *
 * Pixels are bucketed coarsely (4 bits per channel) and each bucket is scored by how often it
 * appears times how saturated it is, so a large flat background loses to a smaller vivid area.
 */
export function pickVividColor(rgba: Uint8ClampedArray): string | null {
  const scores = new Map<
    number,
    { score: number; r: number; g: number; b: number; n: number }
  >()
  for (let i = 0; i < rgba.length; i += 4) {
    const r = rgba[i]!,
      g = rgba[i + 1]!,
      b = rgba[i + 2]!,
      a = rgba[i + 3]!
    if (a < 128) continue
    const max = Math.max(r, g, b),
      min = Math.min(r, g, b)
    const saturation = max === 0 ? 0 : (max - min) / max
    const brightness = max / 255
    if (saturation < 0.25 || brightness < 0.2) continue
    const key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4)
    const entry = scores.get(key) ?? { score: 0, r: 0, g: 0, b: 0, n: 0 }
    entry.score += saturation * brightness
    entry.r += r
    entry.g += g
    entry.b += b
    entry.n += 1
    scores.set(key, entry)
  }
  let best: {
    score: number
    r: number
    g: number
    b: number
    n: number
  } | null = null
  for (const entry of scores.values()) {
    if (!best || entry.score > best.score) best = entry
  }
  if (!best) return null
  const avg = (sum: number) => Math.round(sum / best!.n)
  return `rgb(${avg(best.r)} ${avg(best.g)} ${avg(best.b)})`
}
