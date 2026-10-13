const STEPS = [
  {
    title: 'Drop your mix',
    body: 'MP3, WAV, FLAC or M4A, plus cover art.',
  },
  {
    title: 'Pick the look',
    body: 'Resolution, spin speed and a caption face.',
  },
  { title: 'Download the MP4', body: 'Rendered on the server, ready to post.' },
] as const

/** Three numbered steps under the form, so a first-time visitor knows the whole trip. */
export function HowItWorks() {
  return (
    <ol className="relative mx-auto grid w-full max-w-5xl gap-3 px-6 pb-10 sm:grid-cols-3 sm:px-10">
      {STEPS.map((step, index) => (
        <li
          key={step.title}
          className="flex gap-3 rounded-2xl border border-line bg-white/[0.02] p-4"
        >
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-accent/15 text-xs font-semibold text-accent-glow tabular-nums ring-1 ring-accent/30">
            {index + 1}
          </span>
          <div>
            <p className="text-sm font-semibold text-ink">{step.title}</p>
            <p className="mt-0.5 text-xs text-ink-faint">{step.body}</p>
          </div>
        </li>
      ))}
    </ol>
  )
}
