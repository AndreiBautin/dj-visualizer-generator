/** The caption faces the render offers, with the @font-face family that previews each. */
export const CAPTION_FONT_OPTIONS = [
  {
    value: 'sans-bold',
    label: 'Sans',
    previewFontFamily: "'Caption Sans', sans-serif",
    previewFontWeight: '800',
  },
  {
    value: 'serif-bold',
    label: 'Serif',
    previewFontFamily: "'Caption Serif', serif",
    previewFontWeight: '400',
  },
  {
    value: 'mono-bold',
    label: 'Mono',
    previewFontFamily: "'Caption Mono', monospace",
    previewFontWeight: '700',
  },
] as const
