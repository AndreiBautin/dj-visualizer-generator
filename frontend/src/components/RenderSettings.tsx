import { useFormContext, useWatch } from 'react-hook-form'
import {
  DEFAULT_ROTATION_SPEED_SECONDS,
  MAX_ROTATION_SPEED_SECONDS,
  MIN_ROTATION_SPEED_SECONDS,
  type RenderSettingsInput,
} from '../schemas/renderSettingsSchema'

const CAPTION_FONT_OPTIONS = [
  { value: 'sans-bold', label: 'Sans' },
  { value: 'serif-bold', label: 'Serif' },
  { value: 'mono-bold', label: 'Mono' },
] as const

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
          className="mt-1 w-full rounded-lg border border-white/20 bg-white/5 px-3 py-2 text-white outline-none focus:border-white/50"
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
        <div className="mt-1 flex gap-4">
          <label className="flex items-center gap-2 text-sm text-white/80">
            <input type="radio" value="1080p" {...register('preset')} />
            1080p
          </label>
          <label className="flex items-center gap-2 text-sm text-white/80">
            <input type="radio" value="720p" {...register('preset')} />
            720p
          </label>
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
        <input
          id="rotationSpeedSeconds"
          type="range"
          min={MIN_ROTATION_SPEED_SECONDS}
          max={MAX_ROTATION_SPEED_SECONDS}
          step={0.5}
          className="mt-1 w-full"
          {...register('rotationSpeedSeconds', { valueAsNumber: true })}
        />
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
        <div className="mt-1 flex gap-4">
          {CAPTION_FONT_OPTIONS.map((option) => (
            <label key={option.value} className="flex items-center gap-2 text-sm text-white/80">
              <input type="radio" value={option.value} {...register('captionFont')} />
              {option.label}
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
