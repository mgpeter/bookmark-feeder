/**
 * Where the Auth0 coordinates come from, and why there are two sources.
 *
 * The `web` image is built once and published to Docker Hub, so Vite's build-time `import.meta.env`
 * cannot carry a self-hoster's tenant. In production the nginx entrypoint writes `config.js`, which
 * sets the global below before the app bundle evaluates. In development there is no entrypoint, so
 * `.env.development` fills the gap.
 *
 * None of these values are secret: an OAuth public client id, a tenant domain and an API audience
 * are all published in every SPA bundle by design.
 */
interface RuntimeConfig {
  auth0Domain?: string
  auth0ClientId?: string
  auth0Audience?: string
}

declare global {
  interface Window {
    __BOOKMARKFEEDER_CONFIG__?: RuntimeConfig
  }
}

/**
 * Runtime wins over build-time. Empty strings count as absent, so an unset container variable
 * (which the entrypoint writes as "") falls through instead of producing a broken Auth0Provider.
 */
function pick(runtime: string | undefined, build: string | undefined): string {
  return (runtime ?? '').trim() || (build ?? '').trim()
}

const runtime = window.__BOOKMARKFEEDER_CONFIG__ ?? {}

export const authConfig = {
  domain: pick(runtime.auth0Domain, import.meta.env.VITE_AUTH0_DOMAIN),
  clientId: pick(runtime.auth0ClientId, import.meta.env.VITE_AUTH0_CLIENT_ID),
  audience: pick(runtime.auth0Audience, import.meta.env.VITE_AUTH0_AUDIENCE),
}

/** Names of the variables the deployment forgot to set. Empty when fully configured. */
export function missingAuthConfig(): string[] {
  return [
    !authConfig.domain && 'VITE_AUTH0_DOMAIN',
    !authConfig.clientId && 'VITE_AUTH0_CLIENT_ID',
    !authConfig.audience && 'VITE_AUTH0_AUDIENCE',
  ].filter((name): name is string => Boolean(name))
}

/**
 * Auth0's PKCE flow derives its code challenge with `crypto.subtle`, which browsers expose only on
 * secure origins. `https://` and `localhost` qualify; a plain `http://<nas>:8081` does not, and the
 * SDK fails at construction with an opaque "cannot read properties of undefined (reading 'digest')".
 *
 * Checked up front so that failure is reported as the deployment problem it is.
 */
export function isSecureOrigin(): boolean {
  return window.isSecureContext && typeof window.crypto?.subtle !== 'undefined'
}
