/**
 * A small, deliberate set of surface levels, used instead of every card, input, and panel
 * reaching for its own ad-hoc `bg-white/5` - so elevation reads as a designed system rather than
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
  'rounded-xl border border-white/10 bg-white/[0.04] shadow-sm shadow-black/20'
export const SURFACE_RAISED =
  'rounded-lg border border-white/15 bg-white/[0.07] shadow-md shadow-black/30'
export const SURFACE_SUNKEN =
  'rounded-lg border border-white/10 bg-white/[0.03]'
