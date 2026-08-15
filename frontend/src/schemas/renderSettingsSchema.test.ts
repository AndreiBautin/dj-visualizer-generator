import { describe, expect, it } from 'vitest'
import { renderSettingsSchema } from './renderSettingsSchema'

describe('renderSettingsSchema', () => {
  it('accepts a valid title and preset', () => {
    const result = renderSettingsSchema.safeParse({ title: 'Friday Night Set', preset: '1080p' })

    expect(result.success).toBe(true)
  })

  it('rejects an empty title', () => {
    const result = renderSettingsSchema.safeParse({ title: '   ', preset: '1080p' })

    expect(result.success).toBe(false)
  })

  it('rejects a title longer than 200 characters', () => {
    const result = renderSettingsSchema.safeParse({ title: 'a'.repeat(201), preset: '1080p' })

    expect(result.success).toBe(false)
  })

  it('rejects an unsupported preset', () => {
    const result = renderSettingsSchema.safeParse({ title: 'Set', preset: '4k' })

    expect(result.success).toBe(false)
  })

  it('defaults rotation speed and caption font when omitted', () => {
    const result = renderSettingsSchema.parse({ title: 'Set', preset: '1080p' })

    expect(result.rotationSpeedSeconds).toBe(3)
    expect(result.captionFont).toBe('sans-bold')
  })

  it('accepts a rotation speed within the allowed range', () => {
    const result = renderSettingsSchema.safeParse({ title: 'Set', preset: '1080p', rotationSpeedSeconds: 5.5 })

    expect(result.success).toBe(true)
  })

  it.each([0.5, 15.5])('rejects a rotation speed outside the allowed range (%s)', (value) => {
    const result = renderSettingsSchema.safeParse({ title: 'Set', preset: '1080p', rotationSpeedSeconds: value })

    expect(result.success).toBe(false)
  })

  it.each(['sans-bold', 'serif-bold', 'mono-bold'])('accepts caption font %s', (font) => {
    const result = renderSettingsSchema.safeParse({ title: 'Set', preset: '1080p', captionFont: font })

    expect(result.success).toBe(true)
  })

  it('rejects an unknown caption font', () => {
    const result = renderSettingsSchema.safeParse({ title: 'Set', preset: '1080p', captionFont: 'comic-sans' })

    expect(result.success).toBe(false)
  })
})
