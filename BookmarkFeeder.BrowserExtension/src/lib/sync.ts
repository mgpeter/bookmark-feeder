import { postBatch } from './api'
import { collectFromFolders } from './bookmarks'
import { getSettings, saveSettings } from './settings'
import type { BatchSummary } from './types'

/**
 * Runs a full sync: collect the selected folders, upload them in one batch, and
 * stamp the last-sync time. Shared by the popup and (later) the background worker,
 * so it takes no UI dependencies.
 */
export async function runSync(): Promise<BatchSummary> {
  const { serverUrl, apiKey, selectedFolders } = await getSettings()

  if (!serverUrl) {
    throw new Error('Server URL is not configured — open Settings first.')
  }
  if (selectedFolders.length === 0) {
    throw new Error('Select at least one bookmark folder to sync.')
  }

  const bookmarks = await collectFromFolders(selectedFolders)
  // Deliberately one request: the API's sync policy allows only 5 per minute.
  const { summary } = await postBatch(serverUrl, apiKey, bookmarks)

  await saveSettings({ lastSync: new Date().toISOString() })
  return summary
}
