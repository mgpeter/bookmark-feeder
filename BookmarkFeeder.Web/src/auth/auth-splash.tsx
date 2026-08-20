import { Bookmark } from 'lucide-react'

/**
 * Shown while Auth0 resolves an existing session. Not a bare spinner: `checkSession` on a cold load
 * with a stored refresh token is a real network round-trip, so this is on screen long enough to be
 * noticed, and it uses the app's own palette so there is no flash of a foreign-looking page.
 */
export function AuthSplash({ message = 'Checking your session…' }: { message?: string }) {
  return (
    <div className="flex h-screen flex-col items-center justify-center gap-4 bg-background text-foreground">
      <div className="flex items-center gap-2 text-xl font-semibold">
        <Bookmark className="size-6" />
        BookmarkFeeder
      </div>
      <p className="text-sm text-muted-foreground">{message}</p>
    </div>
  )
}
