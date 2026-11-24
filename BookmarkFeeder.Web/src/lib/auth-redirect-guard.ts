/**
 * Stops a 401 from bouncing the browser through Auth0 forever.
 *
 * A 401 normally means an expired session, and one redirect fixes it. But when the API rejects
 * tokens Auth0 is happy to issue (an audience mismatch between the services, or clock skew), Auth0
 * still has a session and returns at once, the next request 401s again, and the page loops. The
 * in-memory guard in ApiTokenBridge cannot see this: it resets on every page load.
 *
 * So redirects are counted in sessionStorage, which survives the round trip. Once one round trip
 * within the window has failed to produce a working token, the caller stops and shows an error.
 * Any successful API call clears the count.
 */

const STORAGE_KEY = 'bookmarkfeeder.authRedirects'
/** Redirects allowed within the window before giving up. One round trip is enough to tell. */
const MAX_REDIRECTS = 1
/** Long enough to cover a slow Auth0 round trip, short enough not to block a later expiry. */
const WINDOW_MS = 2 * 60_000

interface RedirectRecord {
  count: number
  since: number
}

function read(): RedirectRecord | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<RedirectRecord>
    if (typeof parsed.count !== 'number' || typeof parsed.since !== 'number') return null
    return { count: parsed.count, since: parsed.since }
  } catch {
    return null
  }
}

/**
 * Records a redirect and returns true when it may go ahead; returns false once the limit for the
 * current window has been reached. If sessionStorage is unavailable the redirect is allowed, which
 * is the behaviour without the guard.
 */
export function claimAuthRedirect(now: number = Date.now()): boolean {
  const record = read()
  const current = record && now - record.since < WINDOW_MS ? record : { count: 0, since: now }
  if (current.count >= MAX_REDIRECTS) return false

  try {
    sessionStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ count: current.count + 1, since: current.since }),
    )
  } catch {
    /* storage blocked: fall through and allow the redirect */
  }
  return true
}

/** Called after a successful API call, and before a deliberate retry or sign-out. */
export function clearAuthRedirects(): void {
  try {
    if (sessionStorage.getItem(STORAGE_KEY) !== null) sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    /* storage blocked: nothing to clear */
  }
}
