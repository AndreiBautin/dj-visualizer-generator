import { useRef, type PointerEvent } from 'react'
import { useObjectUrl } from '../hooks/useObjectUrl'
import {
  DEFAULT_LABEL_CROP,
  MAX_LABEL_ZOOM,
  MIN_LABEL_ZOOM,
  clampCrop,
  type LabelCrop,
} from '../lib/labelCrop'
import { BUTTON_GHOST } from '../lib/buttons'
import { CroppedLabelImage } from './CroppedLabelImage'

interface LabelCropEditorProps {
  file: File
  crop: LabelCrop
  onChange: (crop: LabelCrop) => void
}

/**
 * Drag the artwork inside the circle and zoom it to choose the record label. Drag direction
 * follows the image (drag right to see more of its left side), as photo croppers do.
 */
export function LabelCropEditor({
  file,
  crop,
  onChange,
}: LabelCropEditorProps) {
  const url = useObjectUrl(file)
  const drag = useRef<{ x: number; y: number; crop: LabelCrop } | null>(null)
  const frame = useRef<HTMLDivElement>(null)

  function onPointerDown(event: PointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId)
    drag.current = { x: event.clientX, y: event.clientY, crop }
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    const start = drag.current
    const size = frame.current?.clientWidth
    if (!start || !size) return
    // The image's travel is its overhang beyond the frame; at zoom 1 on a square image there
    // is none on that axis, so focus there has no visible effect, as in the render.
    const img = frame.current?.querySelector('img')
    const overhangX = Math.max(1, (img?.clientWidth ?? size) - size)
    const overhangY = Math.max(1, (img?.clientHeight ?? size) - size)
    onChange(
      clampCrop({
        ...start.crop,
        focusX: start.crop.focusX - (event.clientX - start.x) / overhangX,
        focusY: start.crop.focusY - (event.clientY - start.y) / overhangY,
      }),
    )
  }

  if (!url) return null

  return (
    <div className="mt-3 flex items-center gap-4">
      <div
        ref={frame}
        role="img"
        aria-label="Record label crop - drag to reposition"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={() => (drag.current = null)}
        onPointerCancel={() => (drag.current = null)}
        className="relative z-10 h-24 w-24 shrink-0 cursor-grab touch-none overflow-hidden rounded-full ring-2 ring-accent/50 active:cursor-grabbing"
      >
        <CroppedLabelImage src={url} crop={crop} />
      </div>
      <div className="relative z-10 min-w-0 flex-1">
        <label
          htmlFor="label-zoom"
          className="block text-xs font-medium text-ink"
        >
          Label crop
        </label>
        <p className="text-[11px] text-ink-faint">
          Drag the circle to reposition.
        </p>
        <input
          id="label-zoom"
          type="range"
          aria-label="Label zoom"
          min={MIN_LABEL_ZOOM}
          max={MAX_LABEL_ZOOM}
          step={0.05}
          value={crop.zoom}
          onChange={(event) =>
            onChange(clampCrop({ ...crop, zoom: Number(event.target.value) }))
          }
          className="range-accent mt-1 w-full"
          style={
            {
              '--fill': `${((crop.zoom - MIN_LABEL_ZOOM) / (MAX_LABEL_ZOOM - MIN_LABEL_ZOOM)) * 100}%`,
            } as React.CSSProperties
          }
        />
        <button
          type="button"
          onClick={() => onChange(DEFAULT_LABEL_CROP)}
          className={`-ml-3 ${BUTTON_GHOST} py-1 text-xs`}
        >
          Reset
        </button>
      </div>
    </div>
  )
}
