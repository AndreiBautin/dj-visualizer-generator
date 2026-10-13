import '@testing-library/jest-dom/vitest'

// jsdom has no layout, so scrollTo is a "not implemented" stub that logs an error per call.
window.scrollTo = () => {}
