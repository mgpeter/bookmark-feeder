import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * The two ways a self-hosted deployment fails before it can even show a login button. Both render
 * a blank page otherwise, with only an opaque SDK error in the console - so they get a screen that
 * names the problem.
 */
export function MissingConfigScreen({
  missing,
  insecureOrigin,
}: {
  missing: string[]
  insecureOrigin: boolean
}) {
  return (
    <div className="flex h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-xl">
        <CardHeader>
          <CardTitle>BookmarkFeeder is not configured yet</CardTitle>
          <CardDescription>
            Sign-in cannot start until the deployment is finished. See{' '}
            <span className="font-mono">docs/deployment.md</span>.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          {insecureOrigin && (
            <div className="space-y-1">
              <p className="font-medium">This page is not being served over HTTPS.</p>
              <p className="text-muted-foreground">
                Signing in uses PKCE, which needs the browser&apos;s Web Crypto API. Browsers only
                expose it on secure origins, so <span className="font-mono">https://</span> or{' '}
                <span className="font-mono">localhost</span> is required - a plain{' '}
                <span className="font-mono">http://</span> address on your network will not work.
              </p>
              <p className="text-muted-foreground">
                Put TLS in front of the gateway, for example with{' '}
                <span className="font-mono">tailscale serve</span>.
              </p>
            </div>
          )}

          {missing.length > 0 && (
            <div className="space-y-1">
              <p className="font-medium">
                {missing.length === 1 ? 'This variable is' : 'These variables are'} not set on the{' '}
                <span className="font-mono">web</span> service:
              </p>
              <ul className="list-inside list-disc font-mono text-muted-foreground">
                {missing.map((name) => (
                  <li key={name}>{name}</li>
                ))}
              </ul>
              <p className="text-muted-foreground">
                Set them in the <span className="font-mono">.env</span> beside your{' '}
                <span className="font-mono">docker-compose.yaml</span> and restart the stack.
              </p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
