import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

vi.mock('@/config/config-context', () => ({
  getConfig: () => ({ apiKey: 'test-key' }),
}))

import { useMarkAllRead } from './bookmarks'

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('useMarkAllRead', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('posts the active filters as query params with the target state in the body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ updated: 12 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const { result } = renderHook(() => useMarkAllRead(), { wrapper })
    result.current.mutate({
      query: { search: 'graphql', tags: 'dotnet', isRead: false, page: 3, sortBy: 'title' },
      isRead: true,
    })

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    const parsed = new URL(url)

    expect(parsed.pathname).toBe('/api/bookmarks/mark-read')
    // The API's filter param is `search` — `q` is only the web app's own URL param. Sending the
    // wrong name would silently mark the WHOLE collection instead of the filtered set.
    expect(parsed.searchParams.get('search')).toBe('graphql')
    expect(parsed.searchParams.get('tags')).toBe('dotnet')
    expect(parsed.searchParams.get('isRead')).toBe('false')
    // Paging and sorting must not be sent: the action spans every page.
    expect(parsed.searchParams.get('page')).toBeNull()
    expect(parsed.searchParams.get('sortBy')).toBeNull()

    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ isRead: true })
  })

  it('sends no filter params when nothing is filtered', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ updated: 4312 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const { result } = renderHook(() => useMarkAllRead(), { wrapper })
    result.current.mutate({ query: { page: 1, pageSize: 20 }, isRead: true })

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url] = fetchMock.mock.calls[0] as [string]

    expect(new URL(url).search).toBe('')
  })

  it('returns the updated count', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ updated: 12 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )

    const { result } = renderHook(() => useMarkAllRead(), { wrapper })
    result.current.mutate({ query: { search: 'x' }, isRead: true })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({ updated: 12 })
  })
})
