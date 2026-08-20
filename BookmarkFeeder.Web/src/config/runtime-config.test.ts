import { beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * The self-hoster's two failure modes. Both otherwise render a blank page with only an opaque SDK
 * error in the console, so they are worth pinning down.
 */
describe('runtime-config', () => {
  beforeEach(() => {
    vi.resetModules()
    delete window.__BOOKMARKFEEDER_CONFIG__
  })

  it('prefers the runtime config written by the nginx entrypoint', async () => {
    window.__BOOKMARKFEEDER_CONFIG__ = {
      auth0Domain: 'runtime.auth0.com',
      auth0ClientId: 'runtime-client',
      auth0Audience: 'https://runtime.api',
    }

    const { authConfig } = await import('./runtime-config')

    expect(authConfig.domain).toBe('runtime.auth0.com')
    expect(authConfig.clientId).toBe('runtime-client')
    expect(authConfig.audience).toBe('https://runtime.api')
  })

  it('treats an empty runtime value as absent so the build-time fallback applies', async () => {
    // The entrypoint writes "" for a variable the deployment never set. That must fall through
    // rather than producing an Auth0Provider with a blank domain.
    window.__BOOKMARKFEEDER_CONFIG__ = { auth0Domain: '   ', auth0ClientId: '' }

    const { authConfig } = await import('./runtime-config')

    expect(authConfig.domain).toBe(import.meta.env.VITE_AUTH0_DOMAIN ?? '')
  })

  it('names exactly the variables that are missing', async () => {
    window.__BOOKMARKFEEDER_CONFIG__ = {
      auth0Domain: 'set.auth0.com',
      auth0ClientId: '',
      auth0Audience: '',
    }
    vi.stubEnv('VITE_AUTH0_DOMAIN', '')
    vi.stubEnv('VITE_AUTH0_CLIENT_ID', '')
    vi.stubEnv('VITE_AUTH0_AUDIENCE', '')

    const { missingAuthConfig } = await import('./runtime-config')

    expect(missingAuthConfig()).toEqual(['VITE_AUTH0_CLIENT_ID', 'VITE_AUTH0_AUDIENCE'])
    vi.unstubAllEnvs()
  })

  it('reports an insecure origin, which is what breaks PKCE on a plain-HTTP deployment', async () => {
    const { isSecureOrigin } = await import('./runtime-config')

    vi.stubGlobal('isSecureContext', false)
    expect(isSecureOrigin()).toBe(false)

    vi.unstubAllGlobals()
  })
})
