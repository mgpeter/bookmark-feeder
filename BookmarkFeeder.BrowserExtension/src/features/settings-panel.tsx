import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { dashboardUrl, testConnection } from '@/lib/api'
import type { Settings } from '@/lib/types'

interface SettingsPanelProps {
  settings: Settings
  onSave: (patch: Partial<Settings>) => Promise<void>
}

/** Server URL + API key, with a connection check against the API. */
function isLoopback(hostname: string): boolean {
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '[::1]'
}

export function SettingsPanel({ settings, onSave }: SettingsPanelProps) {
  const [serverUrl, setServerUrl] = useState(settings.serverUrl)
  const [apiKey, setApiKey] = useState(settings.apiKey)
  const [testing, setTesting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** Returns the problem with the URL, or null when it's usable. */
  function urlProblem(value: string): string | null {
    if (!value) return 'Enter your server URL, e.g. http://localhost:5180/api'
    let url: URL
    try {
      url = new URL(value)
    } catch {
      return 'Enter a valid URL, e.g. http://localhost:5180/api'
    }
    // "localhost:5180/api" parses, as scheme "localhost:" - and then can never be reached.
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return 'Start the URL with https:// (or http:// for a server on this machine).'
    }
    // The API key is sent with every request; over plain HTTP anyone on the network can read it.
    if (url.protocol === 'http:' && !isLoopback(url.hostname)) {
      return 'Use https:// - over plain http the API key would cross the network unencrypted.'
    }
    return null
  }

  async function save() {
    const url = serverUrl.trim()
    const problem = urlProblem(url)
    if (problem) {
      setError(problem)
      return
    }
    setError(null)
    await onSave({ serverUrl: url, apiKey: apiKey.trim() })
    toast.success('Settings saved')
  }

  async function test() {
    const url = serverUrl.trim()
    const problem = urlProblem(url)
    if (problem) {
      setError(problem)
      return
    }
    setError(null)
    setTesting(true)
    try {
      await testConnection(url, apiKey.trim())
      toast.success('Connection successful')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Connection failed')
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="serverUrl">Server URL</Label>
        <Input
          id="serverUrl"
          value={serverUrl}
          onChange={(e) => setServerUrl(e.target.value)}
          placeholder="http://localhost:5180/api"
        />
        <p className="text-xs text-muted-foreground">
          Your BookmarkFeeder API base URL. The dashboard link uses the same origin.
        </p>
      </div>

      <div className="space-y-2">
        <Label htmlFor="apiKey">API key</Label>
        <Input
          id="apiKey"
          type="password"
          value={apiKey}
          onChange={(e) => setApiKey(e.target.value)}
          placeholder="bmf_…"
        />
        <p className="text-xs text-muted-foreground">
          Sign in to BookmarkFeeder and create a key under{' '}
          <button
            type="button"
            className="underline underline-offset-2 hover:text-foreground"
            onClick={() => {
              const trimmed = serverUrl.trim()
              if (urlProblem(trimmed)) {
                setError(urlProblem(trimmed))
                return
              }
              void chrome.tabs.create({ url: `${dashboardUrl(trimmed)}/settings` })
            }}
          >
            Settings
          </button>
          . It is shown once, so paste it here straight away. Give each browser its own key.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

      {/* Deliberately always clickable: an empty URL reports why rather than
          leaving a dead button next to a placeholder that looks like a value. */}
      <div className="flex gap-2">
        <Button onClick={() => void save()}>Save</Button>
        <Button variant="outline" onClick={() => void test()} disabled={testing}>
          {testing ? 'Testing…' : 'Test connection'}
        </Button>
      </div>
    </div>
  )
}
