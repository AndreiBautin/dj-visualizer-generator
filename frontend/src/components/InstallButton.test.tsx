import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { InstallButton } from './InstallButton'

describe('InstallButton', () => {
  it('stays hidden until the browser offers installation', () => {
    render(<InstallButton />)
    expect(screen.queryByRole('button', { name: /install/i })).toBeNull()
  })

  it('shows once offered, prompts on click, and hides after', async () => {
    const prompt = vi.fn().mockResolvedValue(undefined)
    render(<InstallButton />)
    const event = Object.assign(new Event('beforeinstallprompt'), {
      prompt,
      userChoice: Promise.resolve({ outcome: 'accepted' as const }),
    })
    act(() => {
      window.dispatchEvent(event)
    })

    await userEvent.click(screen.getByRole('button', { name: /install app/i }))

    expect(prompt).toHaveBeenCalledOnce()
    expect(screen.queryByRole('button', { name: /install/i })).toBeNull()
  })
})
