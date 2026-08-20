/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Auth0 tenant domain, without a scheme. Public. */
  readonly VITE_AUTH0_DOMAIN?: string
  /** Auth0 SPA application client id. Public by design in any browser bundle. */
  readonly VITE_AUTH0_CLIENT_ID?: string
  /** The Auth0 API identifier the access token is minted for. */
  readonly VITE_AUTH0_AUDIENCE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
