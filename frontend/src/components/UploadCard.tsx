import { useId, useRef, useState, type DragEvent } from 'react'
import { SURFACE_BASE } from '../lib/surfaces'
import { FilePreview } from './FilePreview'

interface UploadCardProps {
  label: string
  hint: string
  accept: string
  file: File | null
  error?: string | null
  onFileSelected: (file: File) => void
  onClear: () => void
}

export function UploadCard({
  label,
  hint,
  accept,
  file,
  error,
  onFileSelected,
  onClear,
}: UploadCardProps) {
  const inputId = useId()
  const [isDragActive, setIsDragActive] = useState(false)
  // Counts nested enter/leave pairs rather than toggling on either event alone - the dropzone
  // has child elements (the label, the hint text), and a real drag re-fires enter/leave every
  // time the pointer crosses one of their boundaries. A plain boolean would flicker the
  // highlight off each time; the counter only reaches zero once the pointer truly leaves.
  const dragDepth = useRef(0)

  function handleDragEnter(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    dragDepth.current += 1
    setIsDragActive(true)
  }

  function handleDragLeave(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    dragDepth.current = Math.max(0, dragDepth.current - 1)
    if (dragDepth.current === 0) {
      setIsDragActive(false)
    }
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    dragDepth.current = 0
    setIsDragActive(false)
    const droppedFile = event.dataTransfer.files?.[0]
    if (droppedFile) {
      onFileSelected(droppedFile)
    }
  }

  return (
    <div
      data-testid="upload-dropzone"
      data-drag-active={isDragActive}
      onDrop={handleDrop}
      onDragOver={(event) => event.preventDefault()}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      className={`border-dashed p-4 transition-colors ${SURFACE_BASE} ${
        // SURFACE_BASE already sets a border color and background; the ! suffix (Tailwind v4's
        // important modifier) deterministically overrides those two specific utilities for the
        // active-drag state instead of leaving two same-property classes to fight over cascade
        // order.
        isDragActive
          ? 'border-accent/70! bg-accent/10!'
          : 'hover:border-white/25'
      }`}
    >
      <label
        htmlFor={inputId}
        className="cursor-pointer text-sm font-semibold text-white"
      >
        {label}
      </label>
      <input
        id={inputId}
        type="file"
        accept={accept}
        className="sr-only"
        onChange={(event) => {
          const selected = event.target.files?.[0]
          if (selected) {
            onFileSelected(selected)
          }
          event.target.value = ''
        }}
      />

      {file ? (
        <div className="mt-2">
          <FilePreview file={file} onRemove={onClear} />
        </div>
      ) : (
        <p className="mt-1 text-xs text-white/50">{hint}</p>
      )}

      {error && (
        <p role="alert" className="mt-2 text-xs text-red-400">
          {error}
        </p>
      )}
    </div>
  )
}
