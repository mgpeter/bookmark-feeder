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

  it('persists a partial patch to chrome.storage.sync', async () => {
    const { store } = installChromeStub({ storage: { apiKey: 'keep-me' } })

    await saveSettings({ serverUrl: 'http://localhost:5180/api' })

    expect(store).toEqual({ apiKey: 'keep-me', serverUrl: 'http://localhost:5180/api' })
  })
})
