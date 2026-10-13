import { CAPTION_FONT_OPTIONS } from '../lib/captionFonts'

// Drawn, not screenshotted: no demo media or artwork ships in the bundle (see AGENTS.md), and a
// CSS miniature uses the render's own proportions (VinylFilterGraphBuilder): record 74% of the
// frame height, caption 4.4% of it (2.475% of a 16:9 width) with its top 9% above the bottom.
const EXAMPLES = [
  {
    title: 'Late Night Drive',
    label: 'linear-gradient(135deg,#ff4d8d,#6a3cff)',
    font: 'sans-bold',
  },
  {
    title: 'Sunday Dub',
    label: 'linear-gradient(135deg,#ffb347,#ff5e62)',
    font: 'serif-bold',
  },
  {
    title: 'Warehouse 04',
    label: 'linear-gradient(135deg,#00c9a7,#005bea)',
    font: 'mono-bold',
  },
] as const

/** Three miniature results, so a first-time visitor sees the output before uploading. */
export function ExampleStrip() {
  return (
    <section
      aria-label="Example videos"
      className="relative mx-auto w-full max-w-5xl px-6 pb-6 sm:px-10"
    >
      <p className="mb-3 text-xs font-medium tracking-[0.2em] text-ink-faint uppercase">
        What you get
      </p>
      <ul className="grid gap-3 sm:grid-cols-3">
        {EXAMPLES.map((example) => (
          <li key={example.title}>
            <figure
              className="relative aspect-video overflow-hidden rounded-xl bg-[#05080b] ring-1 ring-line-strong"
              // Inline-size containment, so cqw below is "percent of this frame's width". Full size
              // containment would let aspect-ratio collapse the frame to zero height.
              style={{ containerType: 'inline-size' }}
            >
              <div
                aria-hidden="true"
                className="absolute top-1/2 left-1/2 aspect-square h-[74%] -translate-x-1/2 -translate-y-1/2 rounded-full shadow-[0_10px_30px_-10px_rgb(0_0_0/0.9)]"
                style={{
                  background:
                    'repeating-radial-gradient(circle at center, #0b0b0b 0 3px, #161616 3px 4px), #0b0b0b',
                }}
              >
                <div
                  className="absolute inset-[30%] rounded-full"
                  style={{ background: example.label }}
                />
                <div className="absolute inset-[47%] rounded-full bg-black" />
              </div>
              <figcaption
                className="absolute inset-x-0 top-[91%] text-center text-[2.475cqw] leading-none text-white [text-shadow:1px_1px_0_rgb(0_0_0/0.5)]"
                style={{
                  fontFamily: CAPTION_FONT_OPTIONS.find(
                    (o) => o.value === example.font,
                  )?.previewFontFamily,
                }}
              >
                {example.title}
              </figcaption>
            </figure>
          </li>
        ))}
      </ul>
    </section>
  )
}
