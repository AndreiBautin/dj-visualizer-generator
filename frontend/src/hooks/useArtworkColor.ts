import { useEffect, useState } from 'react'
import { pickVividColor } from '../lib/artworkColor'
import { useObjectUrl } from './useObjectUrl'

const SAMPLE_SIZE = 48

/** A vivid colour from the visitor's artwork, sampled from a tiny downscaled copy. */
export function useArtworkColor(file: File | null): string | null {
  const url = useObjectUrl(file)
  const [color, setColor] = useState<string | null>(null)

  useEffect(() => {
    if (!url) {
      setColor(null)
      return
    }
    let cancelled = false
    const image = new Image()
    image.onload = () => {
      const canvas = document.createElement('canvas')
      canvas.width = canvas.height = SAMPLE_SIZE
      const context = canvas.getContext('2d')
      if (!context || cancelled) return
      context.drawImage(image, 0, 0, SAMPLE_SIZE, SAMPLE_SIZE)
      setColor(
        pickVividColor(
          context.getImageData(0, 0, SAMPLE_SIZE, SAMPLE_SIZE).data,
        ),
      )
    }
    image.src = url
    return () => {
      cancelled = true
    }
  }, [url])

  return color
}
