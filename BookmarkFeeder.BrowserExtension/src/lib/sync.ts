import { postBatch } from './api'
import { collectFromFolders } from './bookmarks'
import { getSettings, saveSettings } from './settings'
import { BATCH_SIZE, type BatchSummary } from './types'

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

  // As few requests as possible - the API's sync policy allows only 5 a minute - but no more than
  // BATCH_SIZE bookmarks each, since the API caps a batch. Almost every collection is one request.
  const summary: BatchSummary = { total: 0, created: 0, skipped: 0, errors: 0 }
  for (let start = 0; start === 0 || start < bookmarks.length; start += BATCH_SIZE) {
    const { summary: part } = await postBatch(
      serverUrl,
      apiKey,
      bookmarks.slice(start, start + BATCH_SIZE),
    )
    summary.total += part.total
    summary.created += part.created
    summary.skipped += part.skipped
    summary.errors += part.errors
  }

  await saveSettings({ lastSync: new Date().toISOString() })
  return summary
}
