export const AUDIO_EXTENSIONS = ['.mp3', '.wav', '.flac', '.m4a']
export const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png']
export const MAX_AUDIO_BYTES = 2_147_483_648 // 2 GB
export const MAX_IMAGE_BYTES = 26_214_400 // 25 MB
export const MAX_DURATION_SECONDS = 21_600 // 6 hours

/**
 * The self-hosted defaults, used only until the server's own limits load (see useUploadLimits).
 * These are a display and pre-check convenience; the server is always the authority.
 */
export const DEFAULT_UPLOAD_LIMITS = {
  maxAudioBytes: MAX_AUDIO_BYTES,
  maxImageBytes: MAX_IMAGE_BYTES,
  maxDurationSeconds: MAX_DURATION_SECONDS,
}

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

export function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.round((seconds % 3600) / 60)
  if (hours >= 1)
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours} hours`
  return `${minutes} minutes`
}

export function validateAudioFile(
  file: File,
  maxBytes: number = MAX_AUDIO_BYTES,
): string | null {
  if (!hasExtension(file.name, AUDIO_EXTENSIONS)) {
    return `Unsupported audio format. Use ${AUDIO_EXTENSIONS.join(', ')}.`
  }
  if (file.size > maxBytes) {
    return `Audio file is too large (max ${formatBytes(maxBytes)}).`
  }
  return null
}

export function validateArtworkFile(
  file: File,
  maxBytes: number = MAX_IMAGE_BYTES,
): string | null {
  if (!hasExtension(file.name, IMAGE_EXTENSIONS)) {
    return `Unsupported image format. Use ${IMAGE_EXTENSIONS.join(', ')}.`
  }
  if (file.size > maxBytes) {
    return `Artwork file is too large (max ${formatBytes(maxBytes)}).`
  }
  return null
}
