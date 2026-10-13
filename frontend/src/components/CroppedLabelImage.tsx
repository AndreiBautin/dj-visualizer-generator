import { useState } from 'react'
import { labelImageRect, type LabelCrop } from '../lib/labelCrop'

/** Artwork placed inside a square parent exactly as the render crops it for the label. */
export function CroppedLabelImage({
  src,
  crop,
}: {
  src: string
  crop: LabelCrop
}) {
  const [natural, setNatural] = useState<{ w: number; h: number } | null>(null)
  const rect = natural ? labelImageRect(natural.w, natural.h, crop) : null
  return (
    <img
      src={src}
      alt=""
      draggable={false}
      onLoad={(event) =>
        setNatural({
          w: event.currentTarget.naturalWidth,
          h: event.currentTarget.naturalHeight,
        })
      }
      className="absolute max-w-none select-none"
      style={
        rect
          ? {
              width: `${rect.width * 100}%`,
              height: `${rect.height * 100}%`,
              left: `${rect.left * 100}%`,
              top: `${rect.top * 100}%`,
            }
          : { inset: 0, width: '100%', height: '100%', objectFit: 'cover' }
      }
    />
  )
}
