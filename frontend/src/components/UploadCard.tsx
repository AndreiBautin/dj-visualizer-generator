import { useId, type DragEvent } from 'react'
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

export function UploadCard({ label, hint, accept, file, error, onFileSelected, onClear }: UploadCardProps) {
  const inputId = useId()

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    const droppedFile = event.dataTransfer.files?.[0]
    if (droppedFile) {
      onFileSelected(droppedFile)
    }
  }

  return (
    <div
      data-testid="upload-dropzone"
      onDrop={handleDrop}
      onDragOver={(event) => event.preventDefault()}
      className="rounded-xl border border-dashed border-white/20 bg-white/5 p-4 transition-colors hover:border-white/40"
    >
      <label htmlFor={inputId} className="cursor-pointer text-sm font-semibold text-white">
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
