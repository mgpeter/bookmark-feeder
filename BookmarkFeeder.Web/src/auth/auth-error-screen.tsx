import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

interface Props {
  message: string
  onRetry: () => void
  onSignOut: () => void
}

/**
 * Auth0 refused the session. "Sign out" is the escape hatch that matters: it clears a stuck
 * localStorage cache, which is the only way past a rotated or revoked refresh token.
 */
export function AuthErrorScreen({ message, onRetry, onSignOut }: Props) {
  return (
    <div className="flex h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Could not sign you in</CardTitle>
          <CardDescription>{message}</CardDescription>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button onClick={onRetry}>Try again</Button>
          <Button variant="outline" onClick={onSignOut}>
            Sign out
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
