// Flags frontend exports that no other module imports - a TypeScript-6-safe stand-in for knip,
// which cannot load this compiler. Deliberately simple: names are matched textually against
// import statements across src (tests count as importers), which is enough for a codebase this
// size and never needs the type checker.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

function sourceFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return sourceFiles(path)
    return /\.(ts|tsx)$/.test(name) && !name.endsWith('.d.ts') ? [path] : []
  })
}

export function findUnusedExports(files) {
  const exportPattern =
    /export\s+(?:async\s+)?(?:function|const|class|interface|type)\s+([A-Za-z_$][\w$]*)/g
  const importPattern = /import\s+(?:type\s+)?\{([^}]*)\}\s+from/g
  const imported = new Set()
  for (const { text } of files) {
    for (const match of text.matchAll(importPattern)) {
      for (const part of match[1].split(',')) {
        const name = part
          .trim()
          .replace(/^type\s+/, '')
          .split(/\s+as\s+/)[0]
        if (name) imported.add(name)
      }
    }
  }
  const unused = []
  for (const { path, text } of files) {
    if (/\.test\.tsx?$/.test(path) || /main\.tsx$|vite-env/.test(path)) continue
    for (const match of text.matchAll(exportPattern)) {
      if (!imported.has(match[1])) unused.push(`${path}: ${match[1]}`)
    }
  }
  return unused
}

export function checkUnusedExports(root = 'frontend/src') {
  // main.tsx is the entry point: it imports, but nothing imports it.
  const files = sourceFiles(root).map((path) => ({
    path,
    text: readFileSync(path, 'utf8'),
  }))
  const unused = findUnusedExports(files)
  if (unused.length > 0) {
    throw new Error(
      `Exports nothing imports (drop the export, or the code):\n  ${unused.join('\n  ')}`,
    )
  }
  return 'no unused frontend exports'
}
