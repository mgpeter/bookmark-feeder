import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'

import { api } from '@/lib/api-client'
import { ApiTokenBridge } from './api-token-bridge'

const loginWithRedirect = vi.fn().mockResolvedValue(undefined)

vi.mock('@auth0/auth0-react', () => ({
  useAuth0: () => ({
    isLoading: false,
    isAuthenticated: true,
    error: undefined,
    getAccessTokenSilently: async () => 'token-the-api-rejects',
    loginWithRedirect,
    logout: vi.fn(),
  }),
}))

function respondWith(status: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation(async () =>
      status === 200
        ? new Response('[]', { status, headers: { 'Content-Type': 'application/json' } })
        : new Response('', { status }),
    ),
  )
}

/** Mounts the bridge as a fresh page load would. */
function loadPage() {
  cleanup()
  render(
    <ApiTokenBridge>
      <p>app</p>
    </ApiTokenBridge>,
  )
}

const callApi = () => api.get('/tags').catch(() => undefined)

describe('ApiTokenBridge 401 handling', () => {
  beforeEach(() => {
    sessionStorage.clear()
    loginWithRedirect.mockClear()
  })
  afterEach(cleanup)

  it('redirects to sign in once, then stops and explains when the API keeps refusing', async () => {
    respondWith(401)

    loadPage()
    await callApi()
    expect(loginWithRedirect).toHaveBeenCalledOnce()

    // Auth0 still had a session and came straight back; the reloaded page hits the same 401.
    loadPage()
    await callApi()

    expect(loginWithRedirect).toHaveBeenCalledOnce()
    expect(await screen.findByText(/could not sign you in/i)).toBeInTheDocument()
    expect(screen.getByText(/different Auth0 audiences/i)).toBeInTheDocument()
  })

  it('lets a later expiry redirect again once a call has succeeded', async () => {
    respondWith(401)
    loadPage()
    await callApi()
    expect(loginWithRedirect).toHaveBeenCalledOnce()

    // The round trip worked this time.
    respondWith(200)
    loadPage()
    await callApi()

    respondWith(401)
    await callApi()
    expect(loginWithRedirect).toHaveBeenCalledTimes(2)
  })
})
