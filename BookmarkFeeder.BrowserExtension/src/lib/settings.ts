import type { Settings } from './types'

const DEFAULTS: Settings = {
  serverUrl: '',
  apiKey: '',
  selectedFolders: [],
  lastSync: null,
}

const KEYS = Object.keys(DEFAULTS)

/**
 * Settings live in `chrome.storage.local`, i.e. on this device only. They used to be in
 * `storage.sync`, which Chrome copies to every device signed in to the same Google account: the
 * API key went to all of them (so "revoke one browser's key" broke every browser), and the folder
 * selection - bookmark node ids, which are per-device counters - pointed at different folders on
 * the next machine, which then synced bookmarks the user never chose.
 *
 * Moves anything still in sync storage over once, then removes it there. Local values win.
 */
async function migrateFromSync(): Promise<void> {
  const sync = chrome.storage.sync
  if (!sync) return

  const legacy = await sync.get(KEYS)
  if (Object.keys(legacy).length === 0) return

  const local = await chrome.storage.local.get(KEYS)
  const missing = Object.fromEntries(Object.entries(legacy).filter(([key]) => !(key in local)))
  if (Object.keys(missing).length > 0) await chrome.storage.local.set(missing)
  await sync.remove(KEYS)
}

let migration: Promise<void> | null = null

function migrateOnce(): Promise<void> {
  // A failed migration is retried on the next read rather than cached as done.
  migration ??= migrateFromSync().catch((error: unknown) => {
    migration = null
    throw error
  })
  return migration
}

/** Reads all settings, falling back to defaults for keys that were never stored. */
export async function getSettings(): Promise<Settings> {
  await migrateOnce()
  const stored = await chrome.storage.local.get(KEYS)
  return { ...DEFAULTS, ...stored } as Settings
}

/** Persists a subset of the settings, leaving the rest untouched. */
export async function saveSettings(patch: Partial<Settings>): Promise<void> {
  await migrateOnce()
  await chrome.storage.local.set(patch)
}

/** Test hook: forget that the migration ran. */
export function resetMigrationForTests(): void {
  migration = null
}
