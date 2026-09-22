import { render, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WaveformPreview } from './WaveformPreview'

function makeFile(name: string, size: number, type = 'audio/mpeg'): File {
  const file = new File([new Uint8Array(4)], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

function makeFakeAudioBuffer(samples: number[]): AudioBuffer {
  return {
    getChannelData: () => Float32Array.from(samples),
  } as unknown as AudioBuffer
}

class FakeAudioContext {
  decodeAudioData = vi.fn()
  close = vi.fn().mockResolvedValue(undefined)
}

let fakeContext: FakeAudioContext

beforeEach(() => {
  fakeContext = new FakeAudioContext()
  vi.stubGlobal(
    'AudioContext',
    vi.fn().mockImplementation(function AudioContextMock() {
      return fakeContext
    }),
  )
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('WaveformPreview', () => {
  it('renders nothing while decoding is still in flight', () => {
    fakeContext.decodeAudioData.mockReturnValue(new Promise(() => {}))

    const { container } = render(
      <WaveformPreview file={makeFile('mix.mp3', 5_000_000)} />,
    )

    expect(container.querySelector('svg')).not.toBeInTheDocument()
  })

  it('renders a bar per peak once decoding resolves', async () => {
    fakeContext.decodeAudioData.mockResolvedValue(
      makeFakeAudioBuffer(Array.from({ length: 4800 }, () => 0.5)),
    )

    const { container } = render(
      <WaveformPreview file={makeFile('mix.mp3', 5_000_000)} />,
    )

    await waitFor(() =>
      expect(container.querySelector('svg')).toBeInTheDocument(),
    )
    expect(container.querySelectorAll('rect').length).toBeGreaterThan(0)
  })

  it('renders nothing if decoding fails, rather than showing an error', async () => {
    fakeContext.decodeAudioData.mockRejectedValue(
      new Error('unsupported container'),
    )

    const { container } = render(
      <WaveformPreview file={makeFile('mix.m4a', 5_000_000, 'audio/mp4')} />,
    )

    await waitFor(() => expect(fakeContext.decodeAudioData).toHaveBeenCalled())
    expect(container.querySelector('svg')).not.toBeInTheDocument()
  })

  it('only ever decodes a bounded prefix of the file, regardless of how large it is', async () => {
    fakeContext.decodeAudioData.mockResolvedValue(
      makeFakeAudioBuffer([0.1, 0.2]),
    )
    const file = makeFile('huge-mix.wav', 2_000_000_000, 'audio/wav')
    const sliceSpy = vi.spyOn(file, 'slice')

    render(<WaveformPreview file={file} />)

    await waitFor(() => expect(fakeContext.decodeAudioData).toHaveBeenCalled())
    expect(sliceSpy).toHaveBeenCalledWith(0, expect.any(Number))
    const requestedBytes = sliceSpy.mock.calls[0]![1] as number
    expect(requestedBytes).toBeLessThan(file.size)
  })
})
