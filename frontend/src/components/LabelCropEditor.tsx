import {
  useEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type PointerEvent,
} from 'react'
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
  // Which way the artwork overhangs the square label decides which presets mean anything.
  const [shape, setShape] = useState<'wide' | 'tall' | 'square' | null>(null)
  useEffect(() => {
    if (!url) return
    const image = new Image()
    image.onload = () =>
      setShape(
        image.naturalWidth > image.naturalHeight * 1.05
          ? 'wide'
          : image.naturalHeight > image.naturalWidth * 1.05
            ? 'tall'
            : 'square',
      )
    image.src = url
  }, [url])
  const presets =
    shape === 'wide'
      ? [
          { label: 'Left', crop: { focusX: 0, focusY: 0.5 } },
          { label: 'Centre', crop: { focusX: 0.5, focusY: 0.5 } },
          { label: 'Right', crop: { focusX: 1, focusY: 0.5 } },
        ]
      : shape === 'tall'
        ? [
            { label: 'Top', crop: { focusX: 0.5, focusY: 0 } },
            { label: 'Centre', crop: { focusX: 0.5, focusY: 0.5 } },
            { label: 'Bottom', crop: { focusX: 0.5, focusY: 1 } },
          ]
        : []
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

  // Arrow keys move the window like dragging does (the image moves with the arrow), +/- zoom.
  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const step = event.shiftKey ? 0.2 : 0.05
    const moves: Record<string, Partial<LabelCrop>> = {
      ArrowLeft: { focusX: crop.focusX + step },
      ArrowRight: { focusX: crop.focusX - step },
      ArrowUp: { focusY: crop.focusY + step },
      ArrowDown: { focusY: crop.focusY - step },
      '+': { zoom: crop.zoom + 0.25 },
      '=': { zoom: crop.zoom + 0.25 },
      '-': { zoom: crop.zoom - 0.25 },
    }
    const move = moves[event.key]
    if (!move) return
    event.preventDefault()
    onChange(clampCrop({ ...crop, ...move }))
  }

  if (!url) return null

  return (
    <div className="mt-3 flex items-center gap-4">
      <div
        ref={frame}
        role="group"
        tabIndex={0}
        aria-label="Record label crop"
        aria-describedby="label-crop-help"
        onKeyDown={onKeyDown}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={() => (drag.current = null)}
        onPointerCancel={() => (drag.current = null)}
        className="relative z-10 h-24 w-24 shrink-0 cursor-grab focus-visible:ring-4 focus-visible:ring-accent-glow focus-visible:outline-none touch-none overflow-hidden rounded-full ring-2 ring-accent/50 active:cursor-grabbing"
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
        <p id="label-crop-help" className="text-[11px] text-ink-faint">
          Drag the circle, or focus it and use the arrow keys (+ and − to zoom).
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
        <div className="mt-1 flex flex-wrap items-center gap-1">
          {presets.map((preset) => (
            <button
              key={preset.label}
              type="button"
              aria-pressed={
                Math.abs(crop.focusX - preset.crop.focusX) < 0.01 &&
                Math.abs(crop.focusY - preset.crop.focusY) < 0.01
              }
              onClick={() => onChange(clampCrop({ ...crop, ...preset.crop }))}
              className="rounded-md px-2 py-1 text-[11px] font-medium text-ink-muted ring-1 ring-line transition-colors hover:text-ink aria-pressed:bg-accent aria-pressed:text-accent-content aria-pressed:ring-accent"
            >
              {preset.label}
            </button>
          ))}
          <button
            type="button"
            onClick={() => onChange(DEFAULT_LABEL_CROP)}
            className={`${BUTTON_GHOST} px-2 py-1 text-[11px]`}
          >
            Reset
          </button>
        </div>
      </div>
    </div>
  )
}
