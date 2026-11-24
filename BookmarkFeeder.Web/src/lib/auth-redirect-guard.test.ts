import { beforeEach, describe, expect, it, vi } from 'vitest'
import { claimAuthRedirect, clearAuthRedirects } from './auth-redirect-guard'

describe('auth redirect guard', () => {
  beforeEach(() => sessionStorage.clear())

  it('allows the first redirect and refuses a second within the window', () => {
    expect(claimAuthRedirect(1_000)).toBe(true)
    // The page came back from Auth0 and the API 401'd again: a loop, not an expiry.
    expect(claimAuthRedirect(5_000)).toBe(false)
  })

  it('allows a redirect again once the window has passed', () => {
    expect(claimAuthRedirect(0)).toBe(true)
    expect(claimAuthRedirect(10 * 60_000)).toBe(true)
  })

  it('allows a redirect again after the count is cleared', () => {
    expect(claimAuthRedirect(1_000)).toBe(true)
    clearAuthRedirects()
    expect(claimAuthRedirect(2_000)).toBe(true)
  })

  it('survives a page load, because the count lives in sessionStorage', async () => {
    expect(claimAuthRedirect()).toBe(true)
    // A fresh module instance stands in for the reloaded page.
    vi.resetModules()
    const fresh = await import('./auth-redirect-guard')
    expect(fresh.claimAuthRedirect()).toBe(false)
  })
})
