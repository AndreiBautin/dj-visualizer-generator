// The page accent, read from frontend/src/index.css and converted to sRGB - so checks that
// another surface (the rendered video's backdrop) matches it read the one source of truth.
import { readFileSync } from 'node:fs';

export function accentOklch(css) {
  const match = css.match(/--color-accent:\s*oklch\(([\d.]+)%\s+([\d.]+)\s+([\d.]+)\)/);
  if (!match) throw new Error('--color-accent: oklch(...) not found in index.css');
  return { l: Number(match[1]) / 100, c: Number(match[2]), h: Number(match[3]) };
}

export function oklchToSrgb({ l, c, h }) {
  const a = c * Math.cos((h * Math.PI) / 180);
  const b = c * Math.sin((h * Math.PI) / 180);
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;
  const linear = [
    4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_,
    -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_,
    -0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_,
  ];
  return linear.map((v) => {
    const x = Math.min(1, Math.max(0, v));
    const encoded = x <= 0.0031308 ? 12.92 * x : 1.055 * x ** (1 / 2.4) - 0.055;
    return Math.round(encoded * 255);
  });
}

export function readAccentSrgb() {
  return oklchToSrgb(accentOklch(readFileSync('frontend/src/index.css', 'utf8')));
}
