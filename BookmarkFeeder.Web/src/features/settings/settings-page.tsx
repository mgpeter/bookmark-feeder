import { useState } from 'react'
import { useAuth0 } from '@auth0/auth0-react'
import { toast } from 'sonner'
import { api, ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { ApiKeysCard } from './api-keys-card'

export function SettingsPage() {
  const { user, logout } = useAuth0()
  const [testing, setTesting] = useState(false)

  async function testConnection() {
    setTesting(true)
    try {
      // No manual header: the api-client attaches the bearer token itself.
      await api.get('/tags')
      toast.success('Connection successful')
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server')
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-muted-foreground">Your account and the keys your browsers sync with.</p>
      </div>

      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle>Account</CardTitle>
          <CardDescription>Signed in through Auth0.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex items-center gap-3">
            {user?.picture && (
              <img src={user.picture} alt="" className="size-10 rounded-full" />
            )}
            <div className="min-w-0">
              <p className="truncate font-medium">{user?.name ?? 'Signed in'}</p>
              <p className="truncate text-sm text-muted-foreground">{user?.email}</p>
            </div>
          </div>
          <div className="space-y-1">
            <p className="text-xs text-muted-foreground">
              Your bookmarks are scoped to this identity. Quote it if you ever report a problem.
            </p>
            <code className="block truncate rounded bg-muted px-2 py-1 text-xs">{user?.sub}</code>
          </div>
        </CardContent>
        <CardFooter>
          <Button
            variant="outline"
            onClick={() => void logout({ logoutParams: { returnTo: window.location.origin } })}
          >
            Sign out
          </Button>
        </CardFooter>
      </Card>

      <div className="max-w-3xl">
        <ApiKeysCard />
      </div>

      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle>Connection</CardTitle>
          <CardDescription>
            This app talks to the API at its own origin, so there is no server URL to configure. It
            authenticates with a token from Auth0 that is refreshed for you.
          </CardDescription>
        </CardHeader>
        <CardFooter>
          <Button variant="outline" onClick={testConnection} disabled={testing}>
            {testing ? 'Testing…' : 'Test connection'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
