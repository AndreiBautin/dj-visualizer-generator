// VinylFilterGraphBuilder.FontSizeRatio: caption font size as a fraction of video height.
const FONT_SIZE_RATIO = 0.044
// Every preset is 16:9, so the fit is the same at 720p and 1080p.
const ASPECT = 16 / 9
// drawtext centres the line with no wrapping; past this share of the width it starts to
// crowd the frame edge, and past 1 it is cut off.
const MAX_WIDTH_SHARE = 0.92

/**
 * The caption's width as a share of the video width. `measure` returns the text's width in
 * pixels at a 100px font size in the caption face - injected so this stays a pure function.
 */
export function captionWidthShare(
  text: string,
  measureAt100px: (text: string) => number,
): number {
  return ((measureAt100px(text) / 100) * FONT_SIZE_RATIO) / ASPECT
}

export function captionFits(share: number): boolean {
  return share <= MAX_WIDTH_SHARE
}

/** A canvas-backed measurer for a CSS font family, using the same files the page previews. */
export function canvasMeasurer(fontFamily: string, fontWeight: string) {
  const context = document.createElement('canvas').getContext('2d')
  return (text: string) => {
    if (!context) return 0
    context.font = `${fontWeight} 100px ${fontFamily}`
    return context.measureText(text).width
  }
}
