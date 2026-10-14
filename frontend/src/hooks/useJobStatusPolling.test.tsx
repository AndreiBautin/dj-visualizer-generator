import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useJobStatus } from './useJobStatus'

// Polling runs in background tabs too, so a stop condition that silently stopped stopping would
// poll forever with nothing on screen to show it.
const JOB = {
  jobId: 'a',
  progress: 0,
  errorMessage: null,
  downloadsRemaining: 5,
  preset: '1080p',
  captionFont: 'sans-bold',
  rotationSpeedSeconds: 18,
  labelFocusX: 0.5,
  labelFocusY: 0.5,
  labelZoom: 1,
  title: '',
}

function respondWith(status: number, body: unknown) {
  const fetchMock = vi.fn(
    async () =>
      new Response(JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/json' },
      }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

async function pollFor(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

describe('useJobStatus polling', () => {
  beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }))
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('stops once the job is finished', async () => {
    const fetchMock = respondWith(200, {
      ...JOB,
      status: 'Completed',
      progress: 100,
    })
    renderHook(() => useJobStatus('a'), { wrapper })

    await pollFor(1000)
    const afterFirst = fetchMock.mock.calls.length
    await pollFor(30_000)

    expect(afterFirst).toBe(1)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('stops once the job has expired', async () => {
    const fetchMock = respondWith(404, {
      detail: 'No job exists for the given id.',
    })
    renderHook(() => useJobStatus('a'), { wrapper })

    await pollFor(30_000)

    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('keeps polling while the job is still running', async () => {
    const fetchMock = respondWith(200, {
      ...JOB,
      status: 'Processing',
      progress: 40,
    })
    renderHook(() => useJobStatus('a'), { wrapper })

    await pollFor(30_000)

    expect(fetchMock.mock.calls.length).toBeGreaterThan(3)
  })
})
