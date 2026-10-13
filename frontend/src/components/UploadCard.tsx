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
      className={`relative p-4 transition-[border-color,background-color,box-shadow] duration-200 has-[input:focus-visible]:ring-2 has-[input:focus-visible]:ring-accent/60 ${SURFACE_BASE} ${file ? '' : 'border-dashed'} ${
        // SURFACE_BASE already sets a border color and background; the ! suffix (Tailwind v4's
        // important modifier) deterministically overrides those two specific utilities for the
        // active-drag state instead of leaving two same-property classes to fight over cascade
        // order.
        error && !isDragActive
          ? 'border-danger/60! bg-danger/[0.06]! motion-safe:animate-[shake_380ms_ease-in-out]'
          : isDragActive
            ? 'border-accent/70! bg-accent/10! shadow-[0_0_0_4px_color-mix(in_oklch,var(--color-accent)_15%,transparent)]!'
            : 'hover:border-line-strong hover:bg-tint/[0.05]'
      }`}
    >
      <div className="flex items-center gap-4">
        <div
          aria-hidden="true"
          className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border transition-colors ${
            error
              ? 'border-danger/40 bg-danger/15 text-danger'
              : file
                ? 'border-accent/40 bg-accent/15 text-accent-glow'
                : 'border-line-strong bg-tint/[0.04] text-ink-muted'
          }`}
        >
          {file && !error ? (
            <svg
              viewBox="0 0 20 20"
              className="h-5 w-5 motion-safe:animate-[pop-in_260ms_cubic-bezier(.2,1.4,.4,1)]"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M5 10.5l3.2 3L15 7" />
            </svg>
          ) : (
            <svg
              viewBox="0 0 20 20"
              className="h-5 w-5"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.6"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M10 13V4M6.5 7.5L10 4l3.5 3.5M4 13.5V15a1.5 1.5 0 001.5 1.5h9A1.5 1.5 0 0016 15v-1.5" />
            </svg>
          )}
        </div>
        <div className="min-w-0 flex-1">
          <label
            htmlFor={inputId}
            className="cursor-pointer text-sm font-semibold text-ink after:absolute after:inset-0 after:content-['']"
          >
            {label}
          </label>
          {!file && (
            <p className="mt-0.5 text-xs text-ink-faint">
              <span className="text-ink-muted">
                Drop a file or click to browse
              </span>{' '}
              · <span>{hint}</span>
            </p>
          )}
        </div>
      </div>
      <input
        id={inputId}
        type="file"
        accept={accept}
        className="peer sr-only"
        onKeyDown={(event) => {
          if (event.key === 'Escape' && file) {
            event.preventDefault()
            onClear()
          }
        }}
        onChange={(event) => {
          const selected = event.target.files?.[0]
          if (selected) {
            onFileSelected(selected)
          }
          event.target.value = ''
        }}
      />

      {file && (
        <div className="relative z-10 mt-3">
          <FilePreview file={file} onRemove={onClear} />
        </div>
      )}

      {error && (
        <p
          role="alert"
          className="mt-3 flex items-center gap-1.5 text-xs font-medium text-danger"
        >
          {error}
        </p>
      )}
    </div>
  )
}
