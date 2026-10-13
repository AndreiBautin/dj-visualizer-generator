import { useEffect, useState } from 'react'
import { formatBytes } from '../lib/fileValidation'
import { SURFACE_RAISED } from '../lib/surfaces'
import { WaveformPreview } from './WaveformPreview'

interface FilePreviewProps {
  file: File
  onRemove: () => void
}

export function FilePreview({ file, onRemove }: FilePreviewProps) {
  const [imageUrl, setImageUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!file.type.startsWith('image/')) {
      setImageUrl(null)
      return
    }
    const url = URL.createObjectURL(file)
    setImageUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  return (
    <div className={`flex items-center gap-3 p-3 ${SURFACE_RAISED}`}>
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={file.name}
          className="h-12 w-12 rounded-lg object-cover ring-1 ring-line-strong"
        />
      ) : (
        <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-accent/15 text-[10px] font-semibold tracking-wider text-accent-glow">
          FILE
        </div>
      )}
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-ink">{file.name}</p>
        <p className="text-xs text-ink-muted">{formatBytes(file.size)}</p>
        {file.type.startsWith('audio/') && <WaveformPreview file={file} />}
      </div>
      <button
        type="button"
        onClick={onRemove}
        className="rounded-lg px-2.5 py-1.5 text-xs font-medium text-ink-muted transition-colors hover:bg-tint/10 hover:text-ink focus-visible:ring-2 focus-visible:ring-accent/60 focus-visible:outline-none"
      >
        Remove
      </button>
    </div>
  )
}
