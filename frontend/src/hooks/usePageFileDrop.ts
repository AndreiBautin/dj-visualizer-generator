import { useEffect, useRef, useState } from 'react'

/**
 * Accepts a file dropped anywhere on the page. A drop that a dropzone already handled arrives
 * here with defaultPrevented set and is ignored, so a file is never taken twice.
 */
export function usePageFileDrop(
  onDrop: (file: File) => void,
  enabled: boolean,
) {
  const [isDragging, setIsDragging] = useState(false)
  const depth = useRef(0)
  const onDropRef = useRef(onDrop)
  onDropRef.current = onDrop

  useEffect(() => {
    if (!enabled) return
    const hasFiles = (event: DragEvent) =>
      event.dataTransfer?.types.includes('Files') ?? false

    function enter(event: DragEvent) {
      if (!hasFiles(event)) return
      depth.current += 1
      setIsDragging(true)
    }
    function leave(event: DragEvent) {
      if (!hasFiles(event)) return
      depth.current = Math.max(0, depth.current - 1)
      if (depth.current === 0) setIsDragging(false)
    }
    function over(event: DragEvent) {
      // Without this the browser navigates to the dropped file and the form is lost.
      if (hasFiles(event)) event.preventDefault()
    }
    function drop(event: DragEvent) {
      depth.current = 0
      setIsDragging(false)
      if (event.defaultPrevented) return
      event.preventDefault()
      const file = event.dataTransfer?.files?.[0]
      if (file) onDropRef.current(file)
    }

    window.addEventListener('dragenter', enter)
    window.addEventListener('dragleave', leave)
    window.addEventListener('dragover', over)
    window.addEventListener('drop', drop)
    return () => {
      window.removeEventListener('dragenter', enter)
      window.removeEventListener('dragleave', leave)
      window.removeEventListener('dragover', over)
      window.removeEventListener('drop', drop)
    }
  }, [enabled])

  return isDragging
}
