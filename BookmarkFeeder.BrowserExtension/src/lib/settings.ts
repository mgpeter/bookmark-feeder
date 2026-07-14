import type { Settings } from './types'

const DEFAULTS: Settings = {
  serverUrl: '',
  apiKey: '',
  selectedFolders: [],
  lastSync: null,
}

/** Reads all settings, falling back to defaults for keys that were never stored. */
export async function getSettings(): Promise<Settings> {
  const stored = await chrome.storage.sync.get(Object.keys(DEFAULTS))
  return { ...DEFAULTS, ...stored } as Settings
}

/** Persists a subset of the settings, leaving the rest untouched. */
export async function saveSettings(patch: Partial<Settings>): Promise<void> {
  await chrome.storage.sync.set(patch)
}
