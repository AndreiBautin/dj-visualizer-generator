import { useEffect, useState } from 'react'
import { BUTTON_SECONDARY } from '../lib/buttons'

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

/**
 * Offers "Install app" only where the browser has said installation is possible
 * (beforeinstallprompt - Chromium browsers). Elsewhere, and once installed, it renders nothing:
 * a button that cannot do anything is worse than none.
 */
export function InstallButton() {
  const [deferred, setDeferred] = useState<BeforeInstallPromptEvent | null>(
    null,
  )

  useEffect(() => {
    function onPrompt(event: Event) {
      event.preventDefault()
      setDeferred(event as BeforeInstallPromptEvent)
    }
    function onInstalled() {
      setDeferred(null)
    }
    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  if (!deferred) return null

  return (
    <div className="relative mx-auto flex w-full max-w-5xl justify-center px-6 pb-6 sm:px-10 lg:justify-start">
      <button
        type="button"
        onClick={async () => {
          await deferred.prompt()
          await deferred.userChoice
          // A prompt event is single-use either way.
          setDeferred(null)
        }}
        className={`px-4 py-2 text-xs ${BUTTON_SECONDARY}`}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="h-3.5 w-3.5"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M10 3v9M6.5 8.5L10 12l3.5-3.5M4 15h12" />
        </svg>
        Install app
      </button>
    </div>
  )
}
