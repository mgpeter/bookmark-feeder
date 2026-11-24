import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useAuth0 } from '@auth0/auth0-react'
import { setTokenGetter, setUnauthorizedHandler } from '@/lib/api-client'
import { claimAuthRedirect, clearAuthRedirects } from '@/lib/auth-redirect-guard'
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
  // Set when a sign-in round trip has already failed to fix a 401 (see auth-redirect-guard).
  const [rejected, setRejected] = useState(false)

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
      // Auth0 has a session but the API still refuses its tokens: another redirect would come
      // straight back and loop forever. Stop and say so instead.
      if (!claimAuthRedirect()) {
        setRejected(true)
        return
      }
      redirecting.current = true

      void loginWithRedirect({
        appState: { returnTo: window.location.pathname + window.location.search },
      })
    })
  }, [loginWithRedirect])

  if (isLoading) return <AuthSplash />

  const signOut = () => {
    clearAuthRedirects()
    void logout({ logoutParams: { returnTo: window.location.origin } })
  }

  if (rejected) {
    return (
      <AuthErrorScreen
        message="You signed in, but the server still rejected the session. This usually means the web app and the API are configured with different Auth0 audiences, or the server's clock is wrong. Check the deployment settings, then try again."
        onRetry={() => {
          clearAuthRedirects()
          setRejected(false)
          redirecting.current = true
          void loginWithRedirect({
            appState: { returnTo: window.location.pathname + window.location.search },
          })
        }}
        onSignOut={signOut}
      />
    )
  }

  if (error) {
    return (
      <AuthErrorScreen
        message={error.message}
        onRetry={() => void loginWithRedirect()}
        onSignOut={signOut}
      />
    )
  }

  return <>{children}</>
}
