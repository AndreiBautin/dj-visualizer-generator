export interface LabelCrop {
  focusX: number
  focusY: number
  zoom: number
}

export const DEFAULT_LABEL_CROP: LabelCrop = {
  focusX: 0.5,
  focusY: 0.5,
  zoom: 1,
}
export const MIN_LABEL_ZOOM = 1
export const MAX_LABEL_ZOOM = 4

/**
 * Where the artwork sits inside a square label of side 1, mirroring the render's
 * `scale=S:S:force_original_aspect_ratio=increase,crop=L:L:x=(iw-L)*fx:y=(ih-L)*fy` with
 * S = L * zoom - so the preview label and the burned-in one show the same part of the image.
 */
export function labelImageRect(
  naturalWidth: number,
  naturalHeight: number,
  crop: LabelCrop,
) {
  const cover = crop.zoom / Math.min(naturalWidth, naturalHeight)
  const width = naturalWidth * cover
  const height = naturalHeight * cover
  return {
    width,
    height,
    left: -(width - 1) * crop.focusX,
    top: -(height - 1) * crop.focusY,
  }
}

export function clampCrop(crop: LabelCrop): LabelCrop {
  const clamp = (v: number, lo: number, hi: number) =>
    Math.min(hi, Math.max(lo, v))
  return {
    focusX: clamp(crop.focusX, 0, 1),
    focusY: clamp(crop.focusY, 0, 1),
    zoom: clamp(crop.zoom, MIN_LABEL_ZOOM, MAX_LABEL_ZOOM),
  }
}
