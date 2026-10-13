/**
 * A small, deliberate set of surface levels, used instead of every card, input, and panel
 * reaching for its own ad-hoc `bg-tint/5` - so elevation reads as a designed system rather than
 * Tailwind's un-opinionated defaults left untouched everywhere.
 *
 * - `SURFACE_BASE`: a card sitting directly on the page background (the upload dropzones).
 * - `SURFACE_RAISED`: a card sitting on top of a base surface - a touch more contrast and a
 *   visible shadow, so it reads as physically in front (the selected-file preview inside a
 *   dropzone).
 * - `SURFACE_SUNKEN`: a well something sits inside rather than on top of - form fields and a
 *   segmented control's track.
 */
export const SURFACE_BASE =
  'rounded-2xl border border-line bg-tint/[0.035] shadow-[inset_0_1px_0_0_rgb(255_255_255/0.06),0_8px_24px_-12px_var(--shadow)] backdrop-blur-md'
export const SURFACE_RAISED =
  'rounded-xl border border-line-strong bg-tint/[0.06] shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08),0_10px_30px_-12px_var(--shadow)]'
export const SURFACE_SUNKEN =
  'rounded-xl border border-line bg-sunken shadow-[inset_0_1px_2px_0_var(--shadow-inset)]'
