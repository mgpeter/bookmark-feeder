export function hostname(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url
  }
}

export function origin(url: string): string {
  try {
    return new URL(url).origin
  } catch {
    return ''
  }
}

export function relativeDate(iso: string): string {
  const date = new Date(iso)
  const diff = Date.now() - date.getTime()
  const day = 86_400_000
  if (diff < 0) return date.toLocaleDateString()
  if (diff < day) return 'today'
  if (diff < 2 * day) return 'yesterday'
  if (diff < 7 * day) return `${Math.floor(diff / day)}d ago`
  return date.toLocaleDateString()
}

/**
 * The URL to use as a link target, or undefined when it must not be one. Synced collections
 * contain javascript: bookmarklets (and chrome://, file:// and data: URLs); rendered as an href, a
 * planted bookmarklet would run in the app's origin, where the Auth0 tokens live. Only http(s)
 * becomes a link - everything else is shown as text.
 */
export function safeHref(url: string): string | undefined {
  try {
    const { protocol } = new URL(url)
    return protocol === 'http:' || protocol === 'https:' ? url : undefined
  } catch {
    return undefined
  }
}
