import { useCallback, useEffect, useState } from 'react'
import { getSettings, saveSettings } from '@/lib/settings'
import type { Settings } from '@/lib/types'

/**
 * Loads the persisted settings once and keeps them in sync with storage.
 * `settings` is null until the first read resolves.
 */
export function useSettings() {
  const [settings, setSettings] = useState<Settings | null>(null)

  useEffect(() => {
    void getSettings().then(setSettings)
  }, [])

  const update = useCallback(async (patch: Partial<Settings>) => {
    await saveSettings(patch)
    setSettings((prev) => (prev ? { ...prev, ...patch } : prev))
  }, [])

  /** Re-reads storage, for values written outside this hook (e.g. lastSync by runSync). */
  const reload = useCallback(async () => {
    setSettings(await getSettings())
  }, [])

  return { settings, update, reload }
}
