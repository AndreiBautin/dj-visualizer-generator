import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'

// jsdom has no layout, so scrollTo is a "not implemented" stub that logs an error per call.
window.scrollTo = () => {}

// App writes the job id into the address; without this one test's job leaks into the next.
afterEach(() => {
  window.history.replaceState(null, '', '/')
})
