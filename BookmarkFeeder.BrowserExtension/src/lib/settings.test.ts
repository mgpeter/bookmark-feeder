import { beforeEach, describe, expect, it, vi } from 'vitest'
import { installChromeStub } from '@/test/chrome-stub'
import { getSettings, saveSettings } from './settings'

describe('settings', () => {
  beforeEach(() => vi.unstubAllGlobals())

  it('returns defaults when nothing is stored', async () => {
    installChromeStub()

    await expect(getSettings()).resolves.toEqual({
      serverUrl: '',
      apiKey: '',
      selectedFolders: [],
      lastSync: null,
    })
  })

  it('merges stored values over the defaults', async () => {
    installChromeStub({
      storage: {
        serverUrl: 'http://localhost:5180/api',
        selectedFolders: [{ id: '2', title: 'Dev', depth: 2 }],
      },
    })

    const settings = await getSettings()

    expect(settings.serverUrl).toBe('http://localhost:5180/api')
    expect(settings.selectedFolders).toEqual([{ id: '2', title: 'Dev', depth: 2 }])
    expect(settings.apiKey).toBe('')
    expect(settings.lastSync).toBeNull()
  })

  it('persists a partial patch to chrome.storage.local', async () => {
    const { store } = installChromeStub({ storage: { apiKey: 'keep-me' } })

    await saveSettings({ serverUrl: 'http://localhost:5180/api' })

    expect(store).toEqual({ apiKey: 'keep-me', serverUrl: 'http://localhost:5180/api' })
  })

  it('moves settings out of chrome.storage.sync once, keeping local values', async () => {
    const { store, syncStore } = installChromeStub({
      storage: { serverUrl: 'https://local.example/api' },
      syncStorage: {
        serverUrl: 'https://synced.example/api',
        apiKey: 'synced-key',
        selectedFolders: [{ id: '2', title: 'Dev', depth: 2 }],
      },
    })

    const settings = await getSettings()

    expect(settings.serverUrl).toBe('https://local.example/api')
    expect(settings.apiKey).toBe('synced-key')
    expect(store.selectedFolders).toEqual([{ id: '2', title: 'Dev', depth: 2 }])
    // Gone from sync, so the key stops travelling to the account's other devices.
    expect(syncStore).toEqual({})
  })

  it('migrates before a write, so a save cannot be shadowed by stale synced values', async () => {
    const { store, syncStore } = installChromeStub({ syncStorage: { apiKey: 'old' } })

    await saveSettings({ apiKey: 'new' })

    expect(store.apiKey).toBe('new')
    expect(syncStore).toEqual({})
  })
})
