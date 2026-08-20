import { Auth0Provider, type AppState } from '@auth0/auth0-react'
import type { ReactNode } from 'react'
import { authConfig, isSecureOrigin, missingAuthConfig } from '@/config/runtime-config'
import { router } from '@/routes'
import { MissingConfigScreen } from './missing-config-screen'

/**
 * auth0-react calls this once it has consumed the ?code=&state= parameters and replaceState'd them
 * away. The app uses a DATA router (createBrowserRouter), whose history does not observe that
 * replaceState - so without an explicit navigate the URL bar and the router disagree and a deep
 * link silently lands on "/". router.navigate is used rather than useNavigate because this runs
 * outside the component tree.
 */
function onRedirectCallback(appState?: AppState) {
  void router.navigate(appState?.returnTo ?? '/', { replace: true })
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const missing = missingAuthConfig()
  const insecure = !isSecureOrigin()

  // Bail before constructing Auth0Provider: with a missing domain it throws, and on an insecure
  // origin auth0-spa-js dies reaching for crypto.subtle. Both would render a blank page.
  if (missing.length > 0 || insecure) {
    return <MissingConfigScreen missing={missing} insecureOrigin={insecure} />
  }

  return (
    <Auth0Provider
      domain={authConfig.domain}
      clientId={authConfig.clientId}
      authorizationParams={{
        redirect_uri: window.location.origin,
        audience: authConfig.audience,
        // offline_access is what actually yields a refresh token. useRefreshTokens alone is inert
        // without it, and the symptom is being signed out roughly every hour.
        scope: 'openid profile email offline_access',
      }}
      // Refresh tokens, persisted across reloads. The alternative - a hidden iframe with
      // prompt=none - depends on a third-party cookie on the Auth0 domain, which Chrome and Safari
      // block, so silent auth would fail on every page load. The trade is XSS exposure of the
      // cached token; refresh-token rotation is the mitigation. Recorded in DEC-008.
      useRefreshTokens
      useRefreshTokensFallback={false}
      cacheLocation="localstorage"
      onRedirectCallback={onRedirectCallback}
    >
      {children}
    </Auth0Provider>
  )
}
