import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as apiClient from '../api/client'
import { BuildFooter } from './BuildFooter'

function renderFooter() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <BuildFooter />
    </QueryClientProvider>,
  )
}

describe('BuildFooter', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows the commit the server reports', async () => {
    vi.spyOn(apiClient, 'getVersion').mockResolvedValue({ commit: '7914ae3' })
    renderFooter()

    expect(await screen.findByText('7914ae3')).toBeInTheDocument()
  })

  /**
   * The whole reason this is server-resolved: on the first real deploy the build-time value was
   * never passed, so it sat at its "unknown" default and the footer silently vanished. Rendering
   * nothing is correct here, but only because there is genuinely nothing to show.
   */
  it('renders nothing when neither the server nor the build supplies a commit', async () => {
    vi.spyOn(apiClient, 'getVersion').mockResolvedValue({ commit: null })
    vi.spyOn(apiClient, 'buildSha').mockReturnValue(null)
    const { container } = renderFooter()

    await waitFor(() => expect(apiClient.getVersion).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })

  it('falls back to the build-time sha when the server cannot say', async () => {
    vi.spyOn(apiClient, 'getVersion').mockResolvedValue({ commit: null })
    vi.spyOn(apiClient, 'buildSha').mockReturnValue('local01')
    renderFooter()

    expect(await screen.findByText('local01')).toBeInTheDocument()
  })

  it('survives a server that does not have the endpoint at all', async () => {
    vi.spyOn(apiClient, 'getVersion').mockRejectedValue(
      new apiClient.ApiError('not found', 404),
    )
    vi.spyOn(apiClient, 'buildSha').mockReturnValue('fallback')
    renderFooter()

    expect(await screen.findByText('fallback')).toBeInTheDocument()
  })
})
