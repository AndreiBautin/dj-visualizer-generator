import { useJobStatus } from '../hooks/useJobStatus'
import { CAPTION_FONT_OPTIONS } from './RenderSettings'

/** What this job is rendering, from the server's copy of its settings - correct even when the
 * page was reopened from a link and the form below never held them. */
export function SettingsRecap({ jobId }: { jobId: string }) {
  const { data } = useJobStatus(jobId)
  if (!data) return null
  const font =
    CAPTION_FONT_OPTIONS.find((option) => option.value === data.captionFont)
      ?.label ?? data.captionFont
  return (
    <ul
      aria-label="Render settings"
      className="flex flex-wrap justify-center gap-1.5 text-[11px] text-ink-muted sm:justify-start"
    >
      {[
        data.preset,
        `${font} caption`,
        `${Math.round(data.rotationSpeedSeconds)}s per spin`,
      ].map((item) => (
        <li
          key={item}
          className="rounded-full bg-tint/[0.05] px-2.5 py-1 ring-1 ring-line"
        >
          {item}
        </li>
      ))}
    </ul>
  )
}
