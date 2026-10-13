const SIZE = 64

function iconLink(): HTMLLinkElement | null {
  return document.querySelector<HTMLLinkElement>('link[rel="icon"]')
}

let originalHref: string | null = null

/**
 * Draws the vinyl mark with a progress ring around it into the tab icon, so a backgrounded
 * render can be watched from the tab strip. `null` restores the shipped favicon.
 */
export function setFaviconProgress(progress: number | null): void {
  const link = iconLink()
  if (!link) return
  originalHref ??= link.href
  if (progress === null) {
    link.href = originalHref
    return
  }
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = SIZE
  const context = canvas.getContext('2d')
  if (!context) return
  const center = SIZE / 2
  context.fillStyle = '#05080b'
  context.beginPath()
  context.arc(center, center, 30, 0, Math.PI * 2)
  context.fill()
  context.strokeStyle = 'rgba(255,255,255,0.18)'
  context.lineWidth = 6
  context.beginPath()
  context.arc(center, center, 26, 0, Math.PI * 2)
  context.stroke()
  context.strokeStyle = '#00baed'
  context.lineCap = 'round'
  context.beginPath()
  context.arc(
    center,
    center,
    26,
    -Math.PI / 2,
    -Math.PI / 2 + (Math.PI * 2 * progress) / 100,
  )
  context.stroke()
  context.fillStyle = '#0092e6'
  context.beginPath()
  context.arc(center, center, 12, 0, Math.PI * 2)
  context.fill()
  link.href = canvas.toDataURL('image/png')
}
