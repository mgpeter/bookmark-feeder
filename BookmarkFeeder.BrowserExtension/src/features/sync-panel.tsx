import { useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { runSync } from '@/lib/sync'
import type { BatchSummary, Settings } from '@/lib/types'

interface SyncPanelProps {
  settings: Settings
  onSynced: () => Promise<void>
}

/** Manual sync, last-sync time, and the result of the last run. */
export function SyncPanel({ settings, onSynced }: SyncPanelProps) {
  const [syncing, setSyncing] = useState(false)
  const [summary, setSummary] = useState<BatchSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

  const folderCount = settings.selectedFolders.length

  async function sync() {
    setSyncing(true)
    setError(null)
    setSummary(null)
    try {
      const result = await runSync()
      setSummary(result)
      await onSynced()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sync failed.')
    } finally {
      setSyncing(false)
    }
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        {folderCount === 0
          ? 'No folders selected yet — pick some in the Folders tab.'
          : `Syncing ${folderCount} folder${folderCount === 1 ? '' : 's'}.`}
      </p>

      <Button className="w-full" onClick={() => void sync()} disabled={syncing}>
        <RefreshCw className={syncing ? 'animate-spin' : undefined} />
        {syncing ? 'Syncing…' : 'Sync Now'}
      </Button>

      {summary && (
        <div className="flex flex-wrap gap-1.5">
          <Badge variant="secondary">{summary.created} created</Badge>
          <Badge variant="secondary">{summary.skipped} skipped</Badge>
          {summary.errors > 0 && <Badge variant="destructive">{summary.errors} failed</Badge>}
        </div>
      )}

      {error && <p className="text-sm text-destructive">{error}</p>}

      <p className="text-xs text-muted-foreground">
        {settings.lastSync
          ? `Last synced: ${new Date(settings.lastSync).toLocaleString()}`
          : 'Never synced'}
      </p>
    </div>
  )
}
