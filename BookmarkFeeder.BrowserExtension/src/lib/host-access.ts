/**
 * Asks for access to the configured server only, instead of the manifest claiming every HTTPS
 * site up front (`optional_host_permissions` holds the patterns it may ask for). The API answers
 * CORS for any origin, so requests also work without the grant; the grant is what Safari's
 * per-site access and a stricter future CORS policy rely on.
 *
 * Must be called synchronously from a click handler, before any await: browsers only show the
 * prompt during a user gesture. Already-granted origins resolve true without a prompt.
 */
export function requestHostAccess(serverUrl: string): Promise<boolean> {
  const permissions = typeof chrome !== 'undefined' ? chrome.permissions : undefined
  if (!permissions?.request) return Promise.resolve(true)

  // Match patterns ignore ports, so the host alone covers https://nas.example:8443/api too.
  const { protocol, hostname } = new URL(serverUrl)
  return permissions
    .request({ origins: [`${protocol}//${hostname}/*`] })
    .catch(() => false)
}
