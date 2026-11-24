import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'

import { setTokenGetter } from '@/lib/api-client'
import { BookmarkListPage } from './bookmark-list-page'

setTokenGetter(async () => 'test-token')

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function list(totalItems: number, page = 1, totalPages = 1) {
  return { data: [], pagination: { page, pageSize: 20, totalItems, totalPages } }
}

/**
 * Renders the page at `url`. Every /api/bookmarks request is answered by `bookmarks`, which may
 * return a promise the test resolves later; everything else (tags, categories, searches) is empty.
 */
function setup(url: string, bookmarks: (params: URLSearchParams) => Promise<Response>) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation(async (input: string) => {
      const parsed = new URL(input)
      return parsed.pathname === '/api/bookmarks' ? bookmarks(parsed.searchParams) : json([])
    }),
  )
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/bookmarks', element: <BookmarkListPage /> }], {
    initialEntries: [url],
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

const markAllButton = () => screen.getByRole('button', { name: /mark all as read/i })

describe('BookmarkListPage', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('disables Mark all as read while a new filter is still loading', async () => {
    let releaseFiltered: (r: Response) => void = () => {}
    const router = setup('/bookmarks', (params) =>
      params.get('search')
        ? new Promise((resolve) => (releaseFiltered = resolve))
        : Promise.resolve(json(list(500))),
    )

    await waitFor(() => expect(markAllButton()).toBeEnabled())

    // The old result (500) stays on screen as placeholder data while the search loads.
    await act(() => router.navigate('/bookmarks?q=graphql'))
    expect(markAllButton()).toBeDisabled()

    await act(async () => releaseFiltered(json(list(3))))
    await waitFor(() => expect(markAllButton()).toBeEnabled())
    expect(screen.getByText('3 total')).toBeInTheDocument()
  })

  it('moves a page past the end back to the last real page', async () => {
    const router = setup('/bookmarks?page=3', (params) => {
      const page = Number(params.get('page') ?? 1)
      return Promise.resolve(json(list(25, page, 2)))
    })

    await waitFor(() => expect(router.state.location.search).toBe('?page=2'))
  })
})
