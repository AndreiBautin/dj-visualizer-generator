export const AUDIO_EXTENSIONS = ['.mp3', '.wav', '.flac', '.m4a']
export const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png']
export const MAX_AUDIO_BYTES = 2_147_483_648 // 2 GB
export const MAX_IMAGE_BYTES = 26_214_400 // 25 MB

function hasExtension(fileName: string, extensions: string[]): boolean {
  const lower = fileName.toLowerCase()
  return extensions.some((ext) => lower.endsWith(ext))
}

export function formatBytes(bytes: number): string {
  const gb = bytes / 1_073_741_824
  if (gb >= 1) return `${gb.toFixed(1)} GB`
  const mb = bytes / 1_048_576
  return `${mb.toFixed(0)} MB`
}

export function validateAudioFile(file: File): string | null {
  if (!hasExtension(file.name, AUDIO_EXTENSIONS)) {
    return `Unsupported audio format. Use ${AUDIO_EXTENSIONS.join(', ')}.`
  }
  if (file.size > MAX_AUDIO_BYTES) {
    return `Audio file is too large (max ${formatBytes(MAX_AUDIO_BYTES)}).`
  }
  return null
}

export function validateArtworkFile(file: File): string | null {
  if (!hasExtension(file.name, IMAGE_EXTENSIONS)) {
    return `Unsupported image format. Use ${IMAGE_EXTENSIONS.join(', ')}.`
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return `Artwork file is too large (max ${formatBytes(MAX_IMAGE_BYTES)}).`
  }
  return null
}
