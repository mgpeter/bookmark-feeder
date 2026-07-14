import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { useConfig } from '@/config/config-context'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function SettingsPage() {
  const { apiKey, setConfig } = useConfig()
  const navigate = useNavigate()
  const [key, setKey] = useState(apiKey)
  const [testing, setTesting] = useState(false)

  async function testConnection() {
    setTesting(true)
    try {
      // Same-origin call through the gateway; only the key varies.
      const res = await fetch('/api/tags', { headers: { 'X-API-Key': key } })
      if (res.ok) toast.success('Connection successful')
      else if (res.status === 401) toast.error('Unauthorized — check the API key')
      else toast.error(`Server responded with ${res.status}`)
    } catch {
      toast.error('Could not reach the server')
    } finally {
      setTesting(false)
    }
  }

  function save() {
    setConfig({ apiKey: key.trim() })
    toast.success('Settings saved')
    navigate('/')
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-muted-foreground">Connect this app to your BookmarkFeeder API.</p>
      </div>

      <Card className="max-w-xl">
        <CardHeader>
          <CardTitle>API key</CardTitle>
          <CardDescription>
            The app talks to the API at this site's own origin. Your key is stored
            locally in this browser and sent as the
            <code className="mx-1 rounded bg-muted px-1 py-0.5 text-xs">X-API-Key</code>
            header.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="apiKey">API key</Label>
            <Input
              id="apiKey"
              type="password"
              value={key}
              onChange={(e) => setKey(e.target.value)}
              placeholder="Your X-API-Key value"
            />
          </div>
        </CardContent>
        <CardFooter className="gap-2">
          <Button onClick={save} disabled={!key}>
            Save
          </Button>
          <Button variant="outline" onClick={testConnection} disabled={!key || testing}>
            {testing ? 'Testing…' : 'Test connection'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
