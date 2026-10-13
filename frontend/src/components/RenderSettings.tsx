import { useFormContext, useWatch } from 'react-hook-form'
import { SURFACE_SUNKEN } from '../lib/surfaces'
import {
  DEFAULT_ROTATION_SPEED_SECONDS,
  MAX_ROTATION_SPEED_SECONDS,
  MIN_ROTATION_SPEED_SECONDS,
  type RenderSettingsInput,
} from '../schemas/renderSettingsSchema'

const CAPTION_FONT_OPTIONS = [
  {
    value: 'sans-bold',
    label: 'Sans',
    previewFontFamily: "'Caption Sans', sans-serif",
  },
  {
    value: 'serif-bold',
    label: 'Serif',
    previewFontFamily: "'Caption Serif', serif",
  },
  {
    value: 'mono-bold',
    label: 'Mono',
    previewFontFamily: "'Caption Mono', monospace",
  },
] as const

const PRESET_OPTIONS = [
  { value: '1080p', label: '1080p' },
  { value: '720p', label: '720p' },
] as const

const SEGMENT_LABEL_CLASS =
  'relative z-10 block cursor-pointer rounded-lg px-3 py-2 text-center text-sm text-ink-muted transition-colors duration-200 peer-checked:font-semibold peer-checked:text-accent-content peer-focus-visible:ring-2 peer-focus-visible:ring-white/60 hover:text-ink'

/** The selected-option pill, drawn once per control and slid to the chosen column - so a
 * selection change moves rather than blinks. */
function SegmentPill({ index, count }: { index: number; count: number }) {
  if (index < 0) return null
  return (
    <span
      aria-hidden="true"
      className="absolute inset-y-1 left-1 rounded-lg bg-accent shadow-[0_4px_14px_-4px_var(--color-accent),inset_0_1px_0_rgb(255_255_255/0.25)] transition-transform duration-300 ease-[cubic-bezier(.3,1.3,.5,1)]"
      style={{
        width: `calc((100% - 0.5rem - ${count - 1} * 0.25rem) / ${count})`,
        transform: `translateX(calc(${index} * (100% + 0.25rem)))`,
      }}
    />
  )
}

export function RenderSettings() {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<RenderSettingsInput>()
  const rotationSpeedSeconds = useWatch({
    control,
    name: 'rotationSpeedSeconds',
  })
  const preset = useWatch({ control, name: 'preset' })
  const captionFont = useWatch({ control, name: 'captionFont' })
  const speed = rotationSpeedSeconds ?? DEFAULT_ROTATION_SPEED_SECONDS
  const speedFraction =
    (speed - MIN_ROTATION_SPEED_SECONDS) /
    (MAX_ROTATION_SPEED_SECONDS - MIN_ROTATION_SPEED_SECONDS)

  return (
    <fieldset className="space-y-4">
      <div>
        <label htmlFor="title" className="block text-sm font-semibold text-ink">
          Track title{' '}
          <span className="font-normal text-ink-faint">(optional)</span>
        </label>
        <input
          id="title"
          type="text"
          className={`mt-1.5 w-full px-3.5 py-2.5 text-ink transition-[border-color,box-shadow] outline-none placeholder:text-ink-faint focus:border-accent/60 focus:shadow-[0_0_0_4px_color-mix(in_oklch,var(--color-accent)_18%,transparent)] ${SURFACE_SUNKEN}`}
          placeholder="Leave blank for no caption"
          {...register('title')}
        />
        {errors.title && (
          <p role="alert" className="mt-1 text-xs text-danger">
            {errors.title.message}
          </p>
        )}
      </div>

      <div>
        <span className="block text-sm font-semibold text-ink">
          Video preset
        </span>
        <div
          data-testid="preset-options"
          className={`relative mt-1.5 grid grid-cols-2 gap-1 p-1 ${SURFACE_SUNKEN}`}
        >
          <SegmentPill
            index={PRESET_OPTIONS.findIndex((o) => o.value === preset)}
            count={PRESET_OPTIONS.length}
          />
          {PRESET_OPTIONS.map((option) => (
            <label key={option.value} className="relative">
              <input
                type="radio"
                value={option.value}
                className="peer sr-only"
                {...register('preset')}
              />
              <span className={SEGMENT_LABEL_CLASS}>{option.label}</span>
            </label>
          ))}
        </div>
        {errors.preset && (
          <p role="alert" className="mt-1 text-xs text-danger">
            {errors.preset.message}
          </p>
        )}
      </div>

      <div>
        <label
          htmlFor="rotationSpeedSeconds"
          className="block text-sm font-semibold text-ink"
        >
          Rotation speed
        </label>
        <div className="mt-1 flex items-center gap-2">
          <span className="text-xs text-ink-faint">Fast</span>
          <input
            id="rotationSpeedSeconds"
            type="range"
            min={MIN_ROTATION_SPEED_SECONDS}
            max={MAX_ROTATION_SPEED_SECONDS}
            step={1}
            className="range-accent w-full"
            style={
              { '--fill': `${speedFraction * 100}%` } as React.CSSProperties
            }
            {...register('rotationSpeedSeconds', { valueAsNumber: true })}
          />
          <span className="text-xs text-ink-faint">Slow</span>
        </div>
        <p className="mt-1.5 inline-block rounded-md bg-white/[0.06] px-2 py-0.5 text-xs font-medium text-ink-muted tabular-nums">
          {(rotationSpeedSeconds ?? DEFAULT_ROTATION_SPEED_SECONDS).toFixed(0)}s
          per spin
        </p>
        {errors.rotationSpeedSeconds && (
          <p role="alert" className="mt-1 text-xs text-danger">
            {errors.rotationSpeedSeconds.message}
          </p>
        )}
      </div>

      <div>
        <span className="block text-sm font-semibold text-ink">
          Caption font
        </span>
        <div
          data-testid="caption-font-options"
          className={`relative mt-1.5 grid grid-cols-3 gap-1 p-1 ${SURFACE_SUNKEN}`}
        >
          <SegmentPill
            index={CAPTION_FONT_OPTIONS.findIndex(
              (o) => o.value === captionFont,
            )}
            count={CAPTION_FONT_OPTIONS.length}
          />
          {CAPTION_FONT_OPTIONS.map((option) => (
            <label key={option.value} className="relative">
              <input
                type="radio"
                value={option.value}
                className="peer sr-only"
                {...register('captionFont')}
              />
              <span
                className={SEGMENT_LABEL_CLASS}
                style={{ fontFamily: option.previewFontFamily }}
              >
                {option.label}
              </span>
            </label>
          ))}
        </div>
        {errors.captionFont && (
          <p role="alert" className="mt-1 text-xs text-danger">
            {errors.captionFont.message}
          </p>
        )}
      </div>
    </fieldset>
  )
}
