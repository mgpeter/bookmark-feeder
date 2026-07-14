import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'

vi.mock('@/config/config-context', () => ({
  getConfig: () => ({ apiKey: 'test-key' }),
}))

import { SavedSearches } from './saved-searches'

const saved = [
  { id: 's1', name: 'GraphQL reading', query: 'q=graphql&tags=dotnet', dateCreated: '2026-07-14T10:00:00Z' },
]

function jsonOnce(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

/** Surfaces the current location so tests can assert what a saved search applied. */
function LocationProbe() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname + location.search}</div>
}

function setup(initial = '/bookmarks') {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[initial]}>
        {children}
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>
  )
  render(<SavedSearches currentQuery="q=graphql&tags=dotnet" />, { wrapper })
}

describe('SavedSearches', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('applies a saved search by restoring its filters to the URL', async () => {
    const user = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonOnce(saved)))
    setup()

    await user.click(await screen.findByRole('button', { name: /saved/i }))
    // Exact name: the row also has a "Delete GraphQL reading" button.
    await user.click(await screen.findByRole('button', { name: 'GraphQL reading' }))

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        '/bookmarks?q=graphql&tags=dotnet',
      ),
    )
  })

  it('saves the current query under a name', async () => {
    const user = userEvent.setup()
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonOnce([]))
      .mockResolvedValueOnce(jsonOnce({ id: 'new', name: 'My search', query: 'q=graphql&tags=dotnet' }))
      .mockResolvedValue(jsonOnce([]))
    vi.stubGlobal('fetch', fetchMock)
    setup()

    await user.click(await screen.findByRole('button', { name: /saved/i }))
    await user.type(screen.getByLabelText(/name/i), 'My search')
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(1))
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit]
    expect(new URL(url).pathname).toBe('/api/searches')
    expect(init.method).toBe('POST')
    // The filter string is stored verbatim so applying it later reproduces the view.
    expect(JSON.parse(init.body as string)).toEqual({
      name: 'My search',
      query: 'q=graphql&tags=dotnet',
    })
  })

  it('cannot save without a name', async () => {
    const user = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonOnce([])))
    setup()

    await user.click(await screen.findByRole('button', { name: /saved/i }))

    expect(screen.getByRole('button', { name: /^save$/i })).toBeDisabled()
  })
})
