/** The favicon's vinyl mark, inline so it follows the theme tokens. Kept in step with
 * public/favicon.svg by hand - it is four circles. */
export function BrandMark({ className = 'h-6 w-6' }: { className?: string }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 64 64" className={className}>
      <defs>
        <linearGradient id="brand-label" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="var(--color-accent-glow)" />
          <stop offset="1" stopColor="var(--color-accent)" />
        </linearGradient>
      </defs>
      <circle cx="32" cy="32" r="30" fill="#05080b" />
      <circle
        cx="32"
        cy="32"
        r="30"
        fill="none"
        stroke="white"
        strokeOpacity=".14"
        strokeWidth="1.5"
      />
      <circle
        cx="32"
        cy="32"
        r="22"
        fill="none"
        stroke="white"
        strokeOpacity=".08"
      />
      <circle cx="32" cy="32" r="12" fill="url(#brand-label)" />
      <circle cx="32" cy="32" r="2.5" fill="#05080b" />
    </svg>
  )
}
