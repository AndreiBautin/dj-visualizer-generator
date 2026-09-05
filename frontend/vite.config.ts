/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      // 'prompt': a render can take minutes and is tracked only in page state (no persisted job
      // id), so a service-worker update must never reload out from under an in-progress upload
      // or an active poll - the user has to choose to reload.
      registerType: 'prompt',
      // Registration is done explicitly via the useRegisterSW React hook (UpdatePrompt.tsx) so
      // the "update available" prompt can be an actual UI element, not injectRegister's silent
      // auto-registration.
      injectRegister: false,
      devOptions: { enabled: true },
      manifest: {
        name: 'DJ Visualizer Generator',
        short_name: 'DJ Visualizer',
        description: 'Turn a DJ mix and artwork into a spinning-record video.',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#000000',
        theme_color: '#000000',
        icons: [
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/icons/icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // The API responses (job status, downloads) must never be served from cache - only the
        // built SPA shell and its assets are precached.
        navigateFallbackDenylist: [/^\/jobs\//, /^\/health/, /^\/version/, /^\/limits/],
      },
    }),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
    exclude: ['**/node_modules/**', '**/e2e/**'],
  },
})
