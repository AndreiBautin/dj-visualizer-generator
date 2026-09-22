import { test, expect } from '@playwright/test'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { deflateSync } from 'node:zlib'

function buildSilentWav(durationSeconds: number, sampleRate = 8000): Buffer {
  const bitsPerSample = 16
  const channels = 1
  const blockAlign = channels * (bitsPerSample / 8)
  const dataSize = Math.floor(durationSeconds * sampleRate) * blockAlign
  const byteRate = sampleRate * blockAlign

  const buffer = Buffer.alloc(44 + dataSize)
  buffer.write('RIFF', 0)
  buffer.writeUInt32LE(36 + dataSize, 4)
  buffer.write('WAVE', 8)
  buffer.write('fmt ', 12)
  buffer.writeUInt32LE(16, 16)
  buffer.writeUInt16LE(1, 20) // PCM
  buffer.writeUInt16LE(channels, 22)
  buffer.writeUInt32LE(sampleRate, 24)
  buffer.writeUInt32LE(byteRate, 28)
  buffer.writeUInt16LE(blockAlign, 32)
  buffer.writeUInt16LE(bitsPerSample, 34)
  buffer.write('data', 36)
  buffer.writeUInt32LE(dataSize, 40)
  // Remaining bytes default to zero (silence).
  return buffer
}

function crc32(buf: Buffer): number {
  let crc = ~0
  for (const byte of buf) {
    crc ^= byte
    for (let i = 0; i < 8; i++) {
      crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1
    }
  }
  return ~crc >>> 0
}

function pngChunk(type: string, data: Buffer): Buffer {
  const length = Buffer.alloc(4)
  length.writeUInt32BE(data.length, 0)
  const typeAndData = Buffer.concat([Buffer.from(type, 'ascii'), data])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(typeAndData), 0)
  return Buffer.concat([length, typeAndData, crc])
}

function buildTinyPng(size = 32): Buffer {
  const signature = Buffer.from([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
  ])

  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(size, 0)
  ihdr.writeUInt32BE(size, 4)
  ihdr.writeUInt8(8, 8) // bit depth
  ihdr.writeUInt8(2, 9) // color type: RGB
  ihdr.writeUInt8(0, 10)
  ihdr.writeUInt8(0, 11)
  ihdr.writeUInt8(0, 12)

  const rowBytes = 1 + size * 3
  const raw = Buffer.alloc(rowBytes * size)
  for (let y = 0; y < size; y++) {
    const rowStart = y * rowBytes
    raw[rowStart] = 0 // filter type: none
    for (let x = 0; x < size; x++) {
      const pixelStart = rowStart + 1 + x * 3
      raw[pixelStart] = 200 // R
      raw[pixelStart + 1] = 60 // G
      raw[pixelStart + 2] = 60 // B
    }
  }
  const idatData = deflateSync(raw)

  return Buffer.concat([
    signature,
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', idatData),
    pngChunk('IEND', Buffer.alloc(0)),
  ])
}

test('upload a short mix, watch it render, and download the mp4', async ({
  page,
}) => {
  const workDir = mkdtempSync(join(tmpdir(), 'djvisualizer-e2e-'))
  const audioPath = join(workDir, 'mix.wav')
  const artworkPath = join(workDir, 'cover.png')
  writeFileSync(audioPath, buildSilentWav(3))
  writeFileSync(artworkPath, buildTinyPng())

  await page.goto('/')

  await page.getByLabel('Track title').fill('E2E Happy Path Mix')
  // force: true - the radio is `peer sr-only` (RenderSettings.tsx), so its own visible sibling
  // <span> sits on top of it and intercepts the pointer event a plain .check() sends. A real user
  // clicks that span and the browser's native wrapping-label association checks the input; force
  // skips Playwright's actionability check on the (correctly) invisible input and does the same.
  await page.getByLabel('720p').check({ force: true })
  await page.getByLabel('Audio file').setInputFiles(audioPath)
  await page.getByLabel('Artwork').setInputFiles(artworkPath)

  // Exercise the non-default rotation speed / caption font path through a real render, not just
  // mocked unit tests - proves the full frontend-to-ffmpeg pipeline accepts custom values.
  // Arrow keys drive a genuine native input event (a scripted `.value =` assignment does not -
  // React's change-detection ignores it), matching how a real user nudges the slider.
  //
  // focus(), NOT click(): clicking a range input moves the thumb to the clicked point, so a
  // centre click lands near the middle of the 2-15s range and the arrow presses then start from
  // an unpredictable value rather than the 3s default.
  const rotationSlider = page.getByLabel(/rotation speed/i)
  await rotationSlider.focus()
  await expect(page.getByText(/3\.0s per spin/i)).toBeVisible()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  // step is 0.5, so two presses from the 3s default lands on 4s.
  await expect(page.getByText(/4\.0s per spin/i)).toBeVisible()
  // force: true - same sr-only-radio-under-a-visible-span shape as the preset picker above.
  await page.getByLabel('Mono').check({ force: true })

  await page.getByRole('button', { name: 'Generate Video' }).click()

  // Queued or already Processing - either is a valid immediate next state.
  await expect(page.getByText(/queued|rendering/i)).toBeVisible()

  const downloadLink = page.getByRole('link', { name: /download mp4/i })
  await expect(downloadLink).toBeVisible({ timeout: 120_000 })

  const downloadPromise = page.waitForEvent('download')
  await downloadLink.click()
  const download = await downloadPromise
  expect(download.suggestedFilename()).toMatch(/\.mp4$/)

  await page.getByRole('button', { name: /another/i }).click()
  await expect(
    page.getByRole('button', { name: 'Generate Video' }),
  ).toBeVisible()
})
