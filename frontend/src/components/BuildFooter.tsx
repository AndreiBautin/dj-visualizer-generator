import { buildSha } from '../api/client'

/** Ties the running page to the commit it was built from. Renders nothing in a local dev build,
 * where no SHA is injected. */
export function BuildFooter() {
  const sha = buildSha()
  if (!sha) {
    return null
  }

  return (
    <footer className="text-xs text-white/30">
      build <code>{sha}</code>
    </footer>
  )
}
