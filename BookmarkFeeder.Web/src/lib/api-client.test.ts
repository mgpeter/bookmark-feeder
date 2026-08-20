import { beforeEach, describe, expect, it, vi } from 'vitest'

import { api, setTokenGetter, setUnauthorizedHandler } from './api-client'

// The api-client asks a registered getter for a token rather than reading config directly, so
// tests supply one instead of mocking a module.
setTokenGetter(async () => 'test-token')

describe('api-client', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('builds a relative same-origin /api URL, serializes params, and attaches the bearer token', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await api.get('/bookmarks', { page: 2, search: 'react', empty: '' })

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    const parsed = new URL(url)
    // Same origin as the app, path prefixed with /api.
    expect(parsed.origin).toBe(window.location.origin)
    expect(parsed.pathname).toBe('/api/bookmarks')
    expect(parsed.searchParams.get('page')).toBe('2')
    expect(parsed.searchParams.get('search')).toBe('react')
    expect(parsed.searchParams.get('empty')).toBeNull()
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer test-token')
    expect(init.method).toBe('GET')
  })

  it('sends no Authorization header when there is no token', async () => {
    // Signed out, or called before the token bridge registered. Sending nothing earns an honest
    // 401 instead of a header built from stale state.
    setTokenGetter(async () => null)
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await api.get('/bookmarks')

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined()

    setTokenGetter(async () => 'test-token')
  })

  it('invokes the unauthorized handler and throws on 401', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(api.get('/bookmarks')).rejects.toThrow(/session has expired/i)
    expect(handler).toHaveBeenCalledOnce()
  })
})
