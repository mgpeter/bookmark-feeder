import { useEffect, useRef, type ReactNode } from 'react'
import { useAuth0 } from '@auth0/auth0-react'
import { setTokenGetter, setUnauthorizedHandler } from '@/lib/api-client'
import { AuthSplash } from './auth-splash'
import { AuthErrorScreen } from './auth-error-screen'

/**
 * Connects the React-side Auth0 SDK to the React-free api-client.
 *
 * Nothing below renders while `isLoading` is true, so no query can fire before the effect has
 * registered the getter.
 */
export function ApiTokenBridge({ children }: { children: ReactNode }) {
  const { isLoading, isAuthenticated, error, getAccessTokenSilently, loginWithRedirect, logout } =
    useAuth0()

  // Refs rather than effect dependencies: getAccessTokenSilently is a fresh identity on many
  // renders, and re-registering the getter each time would churn the module state.
  const tokenFn = useRef(getAccessTokenSilently)
  tokenFn.current = getAccessTokenSilently
  const authed = useRef(isAuthenticated)
  authed.current = isAuthenticated

  // The dashboard fires four queries in parallel on a cold load. Single-flight so an expired token
  // triggers ONE refresh: with rotation enabled, concurrent refreshes invalidate each other and
  // Auth0 revokes the whole token family, signing the user out for no visible reason.
  const inFlight = useRef<Promise<string | null> | null>(null)
  const redirecting = useRef(false)

  useEffect(() => {
    setTokenGetter(async () => {
      if (!authed.current) return null
      if (inFlight.current) return inFlight.current

      inFlight.current = tokenFn
        .current()
        // login_required / missing_refresh_token / consent_required all land here. Returning null
        // lets the request go out unauthenticated and 401, which routes into the handler below -
        // one recovery path instead of several.
        .catch(() => null)
        .finally(() => {
          inFlight.current = null
        })

      return inFlight.current
    })

    setUnauthorizedHandler(() => {
      // N parallel 401s must not queue N redirects.
      if (redirecting.current) return
      redirecting.current = true

      void loginWithRedirect({
        appState: { returnTo: window.location.pathname + window.location.search },
      })
    })
  }, [loginWithRedirect])

  if (isLoading) return <AuthSplash />

  if (error) {
    return (
      <AuthErrorScreen
        message={error.message}
        onRetry={() => void loginWithRedirect()}
        onSignOut={() => void logout({ logoutParams: { returnTo: window.location.origin } })}
      />
    )
  }

  return <>{children}</>
}
