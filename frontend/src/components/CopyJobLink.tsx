import { useEffect, useState } from 'react'
import { BUTTON_GHOST } from '../lib/buttons'

/**
 * Copies the current address, which carries the job id. Says plainly that the link is the
 * only key - the app has no accounts, so this must not read as a private share.
 */
export function CopyJobLink() {
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')

  useEffect(() => {
    if (state === 'idle') return
    const timer = setTimeout(() => setState('idle'), 2500)
    return () => clearTimeout(timer)
  }, [state])

  async function copy() {
    try {
      await navigator.clipboard.writeText(window.location.href)
      setState('copied')
    } catch {
      setState('failed')
    }
  }

  return (
    <div className="flex flex-col items-center gap-1 sm:items-start">
      <button
        type="button"
        onClick={copy}
        className={`sm:-ml-3 ${BUTTON_GHOST}`}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="h-4 w-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M8.5 11.5a3 3 0 004.2 0l2.6-2.6a3 3 0 00-4.2-4.2l-.9.9M11.5 8.5a3 3 0 00-4.2 0l-2.6 2.6a3 3 0 004.2 4.2l.9-.9" />
        </svg>
        <span aria-live="polite">
          {state === 'copied'
            ? 'Link copied'
            : state === 'failed'
              ? 'Couldn’t copy — use the address bar'
              : 'Copy link'}
        </span>
      </button>
      <p className="text-[11px] text-ink-faint">
        Anyone with this link can view and download until it expires.
      </p>
    </div>
  )
}
