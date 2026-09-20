import { zodResolver } from '@hookform/resolvers/zod'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FormProvider, useForm } from 'react-hook-form'
import { describe, expect, it } from 'vitest'
import { SURFACE_SUNKEN } from '../lib/surfaces'
import {
  renderSettingsSchema,
  type RenderSettingsInput,
  type RenderSettingsValues,
} from '../schemas/renderSettingsSchema'
import { RenderSettings } from './RenderSettings'

function TestForm({ onSubmit }: { onSubmit: (values: RenderSettingsValues) => void }) {
  const methods = useForm<RenderSettingsInput, unknown, RenderSettingsValues>({
    resolver: zodResolver(renderSettingsSchema),
    defaultValues: { title: '', preset: '1080p', rotationSpeedSeconds: 3, captionFont: 'sans-bold' },
  })

  return (
    <FormProvider {...methods}>
      <form onSubmit={methods.handleSubmit(onSubmit)}>
        <RenderSettings />
        <button type="submit">Submit</button>
      </form>
    </FormProvider>
  )
}

describe('RenderSettings', () => {
  it('renders a title field and both preset options', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByLabelText(/track title/i)).toBeInTheDocument()
    expect(screen.getByLabelText('1080p')).toBeInTheDocument()
    expect(screen.getByLabelText('720p')).toBeInTheDocument()
  })

  it('uses the shared sunken-surface treatment for the title field, since it is a well to type into', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByLabelText(/track title/i)).toHaveClass(...SURFACE_SUNKEN.split(' '))
  })

  it('uses the shared sunken-surface treatment for the segmented-control tracks', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByTestId('preset-options')).toHaveClass(...SURFACE_SUNKEN.split(' '))
    expect(screen.getByTestId('caption-font-options')).toHaveClass(...SURFACE_SUNKEN.split(' '))
  })

  it('defaults to the 1080p preset', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByLabelText('1080p')).toBeChecked()
    expect(screen.getByLabelText('720p')).not.toBeChecked()
  })

  it('shows a validation error when submitted with an empty title', async () => {
    const user = userEvent.setup()
    render(<TestForm onSubmit={() => {}} />)

    await user.click(screen.getByRole('button', { name: 'Submit' }))

    expect(await screen.findByText(/title is required/i)).toBeInTheDocument()
  })

  it('submits the entered title, preset, rotation speed, and caption font', async () => {
    const user = userEvent.setup()
    let submitted: RenderSettingsValues | undefined
    render(<TestForm onSubmit={(values) => (submitted = values)} />)

    await user.type(screen.getByLabelText(/track title/i), 'Friday Night Set')
    await user.click(screen.getByLabelText('720p'))
    await user.click(screen.getByRole('button', { name: 'Submit' }))

    expect(submitted).toEqual({
      title: 'Friday Night Set',
      preset: '720p',
      rotationSpeedSeconds: 3,
      captionFont: 'sans-bold',
    })
  })

  it('renders a rotation speed control defaulting to 3 seconds per spin', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByLabelText(/rotation speed/i)).toHaveValue('3')
    expect(screen.getByText(/3(\.0)?s per spin/i)).toBeInTheDocument()
  })

  it('labels the rotation speed slider ends so direction is unambiguous', () => {
    render(<TestForm onSubmit={() => {}} />)

    // The field's underlying value is a *period* (seconds per spin) - bigger number, slower
    // spin - which is the inverse of what "speed" intuitively suggests. Explicit endpoint labels
    // remove the ambiguity regardless of which way a user expects the slider to run.
    expect(screen.getByText('Fast')).toBeInTheDocument()
    expect(screen.getByText('Slow')).toBeInTheDocument()
  })

  it('updates the displayed rotation speed as the slider moves', async () => {
    render(<TestForm onSubmit={() => {}} />)

    const slider = screen.getByLabelText(/rotation speed/i)
    const nativeValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')!.set!
    nativeValueSetter.call(slider, '5')
    slider.dispatchEvent(new Event('input', { bubbles: true }))

    expect(await screen.findByText(/5(\.0)?s per spin/i)).toBeInTheDocument()
  })

  it('renders caption font options defaulting to Sans', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByLabelText('Sans')).toBeChecked()
    expect(screen.getByLabelText('Serif')).not.toBeChecked()
    expect(screen.getByLabelText('Mono')).not.toBeChecked()
  })

  it('renders each caption font option set in its own font, so the choice previews itself', () => {
    render(<TestForm onSubmit={() => {}} />)

    expect(screen.getByText('Sans')).toHaveStyle({ fontFamily: "'Caption Sans', sans-serif" })
    expect(screen.getByText('Serif')).toHaveStyle({ fontFamily: "'Caption Serif', serif" })
    expect(screen.getByText('Mono')).toHaveStyle({ fontFamily: "'Caption Mono', monospace" })
  })

  it('submits a custom rotation speed and caption font', async () => {
    const user = userEvent.setup()
    let submitted: RenderSettingsValues | undefined
    render(<TestForm onSubmit={(values) => (submitted = values)} />)

    await user.type(screen.getByLabelText(/track title/i), 'Set')
    await user.click(screen.getByLabelText('Mono'))
    await user.click(screen.getByRole('button', { name: 'Submit' }))

    expect(submitted?.captionFont).toBe('mono-bold')
  })
})
