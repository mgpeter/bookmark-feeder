import { beforeEach, describe, expect, it, vi } from 'vitest'
import { installChromeStub, sampleTree } from '@/test/chrome-stub'
import { runSync } from './sync'

const summary = { total: 2, created: 2, skipped: 0, errors: 0 }

function configured(storage: Record<string, unknown> = {}) {
  return installChromeStub({
    tree: sampleTree(),
    storage: {
      serverUrl: 'http://localhost:5180/api',
      apiKey: 'dev-key',
      selectedFolders: [{ id: '2', title: 'Dev', depth: 2 }],
      ...storage,
    },
  })
}

describe('runSync', () => {
  beforeEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('collects the selected folders, posts one batch, and returns the summary', async () => {
    configured()
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ summary }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await expect(runSync()).resolves.toEqual(summary)

    // One request per sync — the API's sync policy only allows 5/minute.
    expect(fetchMock).toHaveBeenCalledOnce()
    const body = JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string)
    expect(body.bookmarks.map((b: { sourceFolder: string }) => b.sourceFolder)).toEqual([
      'Dev',
      'Dev/React',
    ])
  })

  it('records lastSync on success', async () => {
    const { store } = configured()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ summary }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )

    await runSync()

    expect(typeof store.lastSync).toBe('string')
    expect(new Date(store.lastSync as string).getTime()).not.toBeNaN()
  })

  it('refuses to sync when the server URL is not configured', async () => {
    configured({ serverUrl: '' })
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    await expect(runSync()).rejects.toThrow(/server url/i)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('refuses to sync when no folders are selected', async () => {
    configured({ selectedFolders: [] })
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    await expect(runSync()).rejects.toThrow(/folder/i)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('does not stamp lastSync when the sync fails', async () => {
    const { store } = configured()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(runSync()).rejects.toThrow()
    expect(store.lastSync).toBeUndefined()
  })
})
