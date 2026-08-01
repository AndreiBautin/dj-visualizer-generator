import { describe, expect, it } from 'vitest'
import {
  MAX_AUDIO_BYTES,
  MAX_IMAGE_BYTES,
  validateArtworkFile,
  validateAudioFile,
} from './fileValidation'

function makeFile(name: string, size: number, type = ''): File {
  const file = new File([new Uint8Array(Math.min(size, 1024))], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

describe('validateAudioFile', () => {
  it('accepts a supported audio extension within the size limit', () => {
    expect(validateAudioFile(makeFile('mix.mp3', 1024))).toBeNull()
  })

  it('rejects an unsupported extension', () => {
    expect(validateAudioFile(makeFile('mix.exe', 1024))).toMatch(/unsupported/i)
  })

  it('rejects a file over the maximum audio size', () => {
    expect(validateAudioFile(makeFile('mix.mp3', MAX_AUDIO_BYTES + 1))).toMatch(/too large/i)
  })

  it('accepts each supported audio extension', () => {
    for (const ext of ['mp3', 'wav', 'flac', 'm4a']) {
      expect(validateAudioFile(makeFile(`mix.${ext}`, 1024))).toBeNull()
    }
  })
})

describe('validateArtworkFile', () => {
  it('accepts a supported image extension within the size limit', () => {
    expect(validateArtworkFile(makeFile('cover.png', 1024))).toBeNull()
  })

  it('rejects an unsupported extension', () => {
    expect(validateArtworkFile(makeFile('cover.gif', 1024))).toMatch(/unsupported/i)
  })

  it('rejects a file over the maximum image size', () => {
    expect(validateArtworkFile(makeFile('cover.png', MAX_IMAGE_BYTES + 1))).toMatch(/too large/i)
  })

  it('accepts each supported image extension', () => {
    for (const ext of ['jpg', 'jpeg', 'png']) {
      expect(validateArtworkFile(makeFile(`cover.${ext}`, 1024))).toBeNull()
    }
  })
})
