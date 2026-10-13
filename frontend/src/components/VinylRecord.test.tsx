import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { VinylRecord } from './VinylRecord'

function spinningLayer(container: HTMLElement) {
  return container.querySelector<HTMLElement>('[class*="animate-[spin"]')!
}

describe('VinylRecord', () => {
  it('turns at exactly the chosen speed on the form, matching the rendered video', () => {
    const { container } = render(
      <VinylRecord artworkFile={null} rotationSpeedSeconds={12} />,
    )
    expect(
      spinningLayer(container).style.getPropertyValue('--vinyl-duration'),
    ).toBe('12s')
  })

  it('comes to rest once the job is finished', () => {
    const { container } = render(
      <VinylRecord
        artworkFile={null}
        rotationSpeedSeconds={12}
        motion="stopped"
      />,
    )
    expect(spinningLayer(container).style.animationPlayState).toBe('paused')
  })
})
