import { useEffect, useState } from 'react'
import { formatBytes } from '../lib/fileValidation'

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
    <div className="flex items-center gap-3 rounded-lg border border-white/10 bg-white/5 p-3">
      {imageUrl ? (
        <img src={imageUrl} alt={file.name} className="h-12 w-12 rounded object-cover" />
      ) : (
        <div className="flex h-12 w-12 items-center justify-center rounded bg-white/10 text-xs text-white/60">
          FILE
        </div>
      )}
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-white">{file.name}</p>
        <p className="text-xs text-white/60">{formatBytes(file.size)}</p>
      </div>
      <button
        type="button"
        onClick={onRemove}
        className="rounded px-2 py-1 text-xs text-white/60 hover:bg-white/10 hover:text-white"
      >
        Remove
      </button>
    </div>
  )
}
