import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/config/config-context', () => ({
  getConfig: () => ({ apiKey: 'secret-key' }),
}))

import { api, setUnauthorizedHandler } from './api-client'

describe('api-client', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('builds a relative same-origin /api URL, serializes params, and attaches X-API-Key', async () => {
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
    expect((init.headers as Record<string, string>)['X-API-Key']).toBe('secret-key')
    expect(init.method).toBe('GET')
  })

  it('invokes the unauthorized handler and throws on 401', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(api.get('/bookmarks')).rejects.toThrow(/unauthorized/i)
    expect(handler).toHaveBeenCalledOnce()
  })
})
