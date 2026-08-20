import { useEffect, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth0 } from '@auth0/auth0-react'
import { AuthSplash } from './auth-splash'

/**
 * The route guard. Replaces RequireConfig, which used to redirect to Settings until an API key had
 * been pasted.
 *
 * Not a <Navigate>: there is no local login route, so the whole page leaves for Auth0 and comes
 * back. `returnTo` is what makes a deep link survive that round trip.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isLoading, isAuthenticated, loginWithRedirect } = useAuth0()
  const location = useLocation()

  useEffect(() => {
    if (isLoading || isAuthenticated) return

    void loginWithRedirect({
      appState: { returnTo: location.pathname + location.search },
    })
  }, [isLoading, isAuthenticated, loginWithRedirect, location])

  if (isLoading) return <AuthSplash />
  if (!isAuthenticated) return <AuthSplash message="Redirecting you to sign in…" />

  return <>{children}</>
}
