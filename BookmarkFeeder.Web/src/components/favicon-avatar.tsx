import { useState } from 'react'
import { hostname, origin, safeHref } from '@/lib/format'
import { cn } from '@/lib/utils'

interface Props {
  url: string
  faviconUrl?: string | null
  className?: string
}

/**
 * Shows the site favicon, falling back to a domain monogram. Only ever requests the
 * bookmarked site's own /favicon.ico (or a stored faviconUrl) — never a third-party service.
 */
export function FaviconAvatar({ url, faviconUrl, className }: Props) {
  const [failed, setFailed] = useState(false)
  const host = hostname(url)
  const letter = (host[0] ?? '?').toUpperCase()
  const src = faviconSrc(url, faviconUrl)

  if (!src || failed) {
    return (
      <div
        className={cn(
          'flex h-8 w-8 shrink-0 items-center justify-center rounded bg-muted text-xs font-semibold text-muted-foreground',
          className,
        )}
        aria-hidden
      >
        {letter}
      </div>
    )
  }

  return (
    <img
      src={src}
      alt=""
      loading="lazy"
      onError={() => setFailed(true)}
      className={cn('h-8 w-8 shrink-0 rounded object-contain', className)}
    />
  )
}

/**
 * A stored faviconUrl is used as given. Otherwise only an http(s) bookmark is worth asking: a
 * javascript:, chrome: or file: URL has the opaque origin "null", so deriving /favicon.ico from it
 * requested "/null/favicon.ico" from this app instead. Those go straight to the monogram.
 */
function faviconSrc(url: string, faviconUrl?: string | null): string {
  if (faviconUrl) return faviconUrl
  return safeHref(url) ? `${origin(url)}/favicon.ico` : ''
}
