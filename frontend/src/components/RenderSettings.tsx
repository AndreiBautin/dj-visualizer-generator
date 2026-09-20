import { useFormContext, useWatch } from 'react-hook-form'
import { SURFACE_SUNKEN } from '../lib/surfaces'
import {
  DEFAULT_ROTATION_SPEED_SECONDS,
  MAX_ROTATION_SPEED_SECONDS,
  MIN_ROTATION_SPEED_SECONDS,
  type RenderSettingsInput,
} from '../schemas/renderSettingsSchema'

const CAPTION_FONT_OPTIONS = [
  { value: 'sans-bold', label: 'Sans', previewFontFamily: "'Caption Sans', sans-serif" },
  { value: 'serif-bold', label: 'Serif', previewFontFamily: "'Caption Serif', serif" },
  { value: 'mono-bold', label: 'Mono', previewFontFamily: "'Caption Mono', monospace" },
] as const

const PRESET_OPTIONS = [
  { value: '1080p', label: '1080p' },
  { value: '720p', label: '720p' },
] as const

const SEGMENT_LABEL_CLASS =
  'block cursor-pointer rounded-md px-3 py-2 text-center text-sm text-white/70 transition-colors peer-checked:bg-white peer-checked:font-semibold peer-checked:text-black hover:text-white'

export function RenderSettings() {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<RenderSettingsInput>()
  const rotationSpeedSeconds = useWatch({ control, name: 'rotationSpeedSeconds' })

  return (
    <fieldset className="space-y-4">
      <div>
        <label htmlFor="title" className="block text-sm font-semibold text-white">
          Track title
        </label>
        <input
          id="title"
          type="text"
          className={`mt-1 w-full px-3 py-2 text-white outline-none focus:border-white/50 ${SURFACE_SUNKEN}`}
          {...register('title')}
        />
        {errors.title && (
          <p role="alert" className="mt-1 text-xs text-red-400">
            {errors.title.message}
          </p>
        )}
      </div>

      <div>
        <span className="block text-sm font-semibold text-white">Video preset</span>
        <div data-testid="preset-options" className={`mt-1 grid grid-cols-2 gap-1 p-1 ${SURFACE_SUNKEN}`}>
          {PRESET_OPTIONS.map((option) => (
            <label key={option.value}>
              <input type="radio" value={option.value} className="peer sr-only" {...register('preset')} />
              <span className={SEGMENT_LABEL_CLASS}>{option.label}</span>
            </label>
          ))}
        </div>
        {errors.preset && (
          <p role="alert" className="mt-1 text-xs text-red-400">
            {errors.preset.message}
          </p>
        )}
      </div>

      <div>
        <label htmlFor="rotationSpeedSeconds" className="block text-sm font-semibold text-white">
          Rotation speed
        </label>
        <div className="mt-1 flex items-center gap-2">
          <span className="text-xs text-white/50">Fast</span>
          <input
            id="rotationSpeedSeconds"
            type="range"
            min={MIN_ROTATION_SPEED_SECONDS}
            max={MAX_ROTATION_SPEED_SECONDS}
            step={0.5}
            className="w-full accent-white"
            {...register('rotationSpeedSeconds', { valueAsNumber: true })}
          />
          <span className="text-xs text-white/50">Slow</span>
        </div>
        <p className="mt-1 text-xs text-white/60">
          {(rotationSpeedSeconds ?? DEFAULT_ROTATION_SPEED_SECONDS).toFixed(1)}s per spin
        </p>
        {errors.rotationSpeedSeconds && (
          <p role="alert" className="mt-1 text-xs text-red-400">
            {errors.rotationSpeedSeconds.message}
          </p>
        )}
      </div>

      <div>
        <span className="block text-sm font-semibold text-white">Caption font</span>
        <div data-testid="caption-font-options" className={`mt-1 grid grid-cols-3 gap-1 p-1 ${SURFACE_SUNKEN}`}>
          {CAPTION_FONT_OPTIONS.map((option) => (
            <label key={option.value}>
              <input type="radio" value={option.value} className="peer sr-only" {...register('captionFont')} />
              <span className={SEGMENT_LABEL_CLASS} style={{ fontFamily: option.previewFontFamily }}>
                {option.label}
              </span>
            </label>
          ))}
        </div>
        {errors.captionFont && (
          <p role="alert" className="mt-1 text-xs text-red-400">
            {errors.captionFont.message}
          </p>
        )}
      </div>
    </fieldset>
  )
}
