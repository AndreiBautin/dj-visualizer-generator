// Fails when the built entry bundle outgrows its budget, so growth is a decision someone makes
// rather than something found later in a Lighthouse score. Measured gzipped, as it is served.
import { readdirSync, readFileSync } from 'node:fs'
import { gzipSync } from 'node:zlib'

// 2026-10-14: the entry chunk was 113 KB gzipped. The budget leaves room for ordinary features;
// raising it should come with a reason in the commit message.
export const ENTRY_BUDGET_KB = 130

export function entryGzipKb(assetsDir) {
  const entry = readdirSync(assetsDir).find((name) =>
    /^index-.*\.js$/.test(name),
  )
  if (!entry) throw new Error(`No index-*.js entry bundle in ${assetsDir}`)
  return gzipSync(readFileSync(`${assetsDir}/${entry}`)).length / 1024
}

export function checkBudget(sizeKb, budgetKb = ENTRY_BUDGET_KB) {
  if (sizeKb > budgetKb) {
    throw new Error(
      `Entry bundle is ${sizeKb.toFixed(1)} KB gzipped, over the ${budgetKb} KB budget (scripts/bundle-budget.mjs).`,
    )
  }
  return `entry bundle ${sizeKb.toFixed(1)} KB gzipped (budget ${budgetKb} KB)`
}
