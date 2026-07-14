import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, dashboardUrl, postBatch, testConnection } from './api'
import type { BatchBookmarkItem } from './types'

const items: BatchBookmarkItem[] = [
  {
    url: 'https://vite.dev',
    title: 'Vite',
    description: null,
    sourceFolder: 'Dev',
    dateAdded: 1700000000000,
  },
]

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const summary = { total: 1, created: 1, skipped: 0, errors: 0 }

describe('dashboardUrl', () => {
  it.each([
    ['http://localhost:5180/api', 'http://localhost:5180'],
    ['http://localhost:5180/api/', 'http://localhost:5180'],
    ['https://bookmarks.example.com/api', 'https://bookmarks.example.com'],
    // Already the web origin — nothing to strip.
    ['http://localhost:5180', 'http://localhost:5180'],
  ])('derives the dashboard origin from %s', (serverUrl, expected) => {
    expect(dashboardUrl(serverUrl)).toBe(expected)
  })
})

describe('postBatch', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('posts the batch payload to /bookmarks/batch with the API key', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ summary }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await postBatch('http://localhost:5180/api/', 'secret', items)

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('http://localhost:5180/api/bookmarks/batch')
    expect(init.method).toBe('POST')
    expect((init.headers as Record<string, string>)['X-API-Key']).toBe('secret')
    expect(JSON.parse(init.body as string)).toEqual({
      bookmarks: items,
      defaultTags: [],
      skipDuplicates: true,
    })
    expect(result.summary).toEqual(summary)
  })

  it('throws a 401 ApiError pointing at the API key', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(postBatch('http://x/api', 'bad', items)).rejects.toThrow(/api key/i)
  })

  // The API's sync policy allows 5 requests/minute and returns Retry-After.
  it('surfaces the retry delay on 429', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('', { status: 429, headers: { 'Retry-After': '42' } })),
    )

    await expect(postBatch('http://x/api', 'k', items)).rejects.toThrow(/42 seconds/i)
  })

  it('throws with the server message on a validation failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse({ message: 'Url is too long' }, 400)),
    )

    const error = await postBatch('http://x/api', 'k', items).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).message).toBe('Url is too long')
  })
})

describe('testConnection', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('GETs /tags with the API key', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse([]))
    vi.stubGlobal('fetch', fetchMock)

    await testConnection('http://localhost:5180/api', 'secret')

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('http://localhost:5180/api/tags')
    expect(init.method).toBe('GET')
    expect((init.headers as Record<string, string>)['X-API-Key']).toBe('secret')
  })

  it('rejects when the key is wrong', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(testConnection('http://x/api', 'bad')).rejects.toThrow(/api key/i)
  })

  it('reports an unreachable server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(testConnection('http://x/api', 'k')).rejects.toThrow(/could not reach/i)
  })
})
