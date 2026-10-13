/**
 * Three button weights, so every screen ranks its actions the same way instead of each one
 * restyling "the main button" and "the other button" from scratch.
 *
 * - `BUTTON_PRIMARY`: the one thing this screen is for (Generate, Download).
 * - `BUTTON_SECONDARY`: a real alternative path (render a sample, start over after an error).
 * - `BUTTON_GHOST`: a quiet escape hatch that should not compete (Create another video).
 */
const BUTTON_BASE =
  'inline-flex items-center justify-center gap-2 rounded-xl px-6 py-3 text-sm font-semibold transition-[transform,filter,background-color,box-shadow,opacity] duration-150 focus-visible:ring-2 focus-visible:ring-accent-glow focus-visible:ring-offset-2 focus-visible:ring-offset-canvas focus-visible:outline-none active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-40 disabled:active:scale-100'

export const BUTTON_PRIMARY = `${BUTTON_BASE} bg-gradient-to-b from-accent-glow to-accent text-accent-content shadow-[0_10px_30px_-10px_var(--color-accent),inset_0_1px_0_rgb(255_255_255/0.3)] hover:-translate-y-px hover:brightness-110 disabled:hover:translate-y-0 disabled:hover:brightness-100`

export const BUTTON_SECONDARY = `${BUTTON_BASE} border border-line-strong bg-white/[0.04] text-ink hover:bg-white/[0.08]`

export const BUTTON_GHOST = `${BUTTON_BASE} px-3 py-2 font-medium text-ink-muted hover:bg-white/[0.06] hover:text-ink`
