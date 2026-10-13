import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { usePageFileDrop } from './usePageFileDrop'

function dropEvent(file: File, alreadyHandled = false) {
  const event = new Event('drop', { cancelable: true }) as DragEvent
  Object.defineProperty(event, 'dataTransfer', {
    value: { files: [file], types: ['Files'] },
  })
  if (alreadyHandled) event.preventDefault()
  return event
}

describe('usePageFileDrop', () => {
  const file = new File(['x'], 'mix.mp3', { type: 'audio/mpeg' })

  it('takes a file dropped outside any dropzone', () => {
    const onDrop = vi.fn()
    renderHook(() => usePageFileDrop(onDrop, true))

    act(() => {
      window.dispatchEvent(dropEvent(file))
    })

    expect(onDrop).toHaveBeenCalledWith(file)
  })

  it('leaves a drop a dropzone already handled alone, so the file is not taken twice', () => {
    const onDrop = vi.fn()
    renderHook(() => usePageFileDrop(onDrop, true))

    act(() => {
      window.dispatchEvent(dropEvent(file, true))
    })

    expect(onDrop).not.toHaveBeenCalled()
  })
})
