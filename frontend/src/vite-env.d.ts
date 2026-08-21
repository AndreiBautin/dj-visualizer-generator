/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  /** "true" enables the one-click sample-mix button. Must match the server's Demo__Enabled. */
  readonly VITE_SAMPLE_ENABLED?: string
  /** Injected at build time so a deployed page can be tied back to a commit. */
  readonly VITE_BUILD_SHA?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
