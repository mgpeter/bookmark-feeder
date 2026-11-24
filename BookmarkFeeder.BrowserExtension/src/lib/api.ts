import type { BatchBookmarkItem, BatchResult } from './types'

/**
 * `status` is the HTTP status, or 0 when the server could not be reached at all. `notTheApi` marks
 * a server that answered but is not the API (a Server URL missing /api) - reachable, so not 0.
 */
export class ApiError extends Error {
  status: number
  notTheApi: boolean

  constructor(status: number, message: string, notTheApi = false) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.notTheApi = notTheApi
  }
}

/** Set by `vite build --mode safari`; Chrome and Edge users never need Safari's site grant. */
const IS_SAFARI_BUILD = import.meta.env.MODE === 'safari'

/** Strips trailing slashes so paths can be appended safely. */
function normalize(serverUrl: string): string {
  return serverUrl.trim().replace(/\/+$/, '')
}

/**
 * Derives the dashboard URL from the base API URL by dropping a trailing `/api`
 * (the web app and the API share an origin behind the gateway).
 */
export function dashboardUrl(serverUrl: string): string {
  return normalize(serverUrl).replace(/\/api$/i, '')
}

async function request(
  method: string,
  serverUrl: string,
  path: string,
  apiKey: string,
  body?: unknown,
): Promise<Response> {
  let res: Response
  try {
    res = await fetch(`${normalize(serverUrl)}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': apiKey,
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    // fetch only rejects on network-level failures (server down, DNS, CORS). In Safari it also
    // rejects when the user has not granted the extension access to the server's site.
    throw new ApiError(
      0,
      'Could not reach the server — check the URL and that it is running.' +
        (IS_SAFARI_BUILD
          ? ' Also allow BookmarkFeeder to access this site (Safari Settings → Extensions).'
          : ''),
    )
  }

  if (res.ok) return res

  if (res.status === 401) {
    throw new ApiError(
      401,
      'Key rejected — it may have been revoked or expired. Create a new one in BookmarkFeeder Settings.',
    )
  }
  if (res.status === 403) {
    // API keys are sync-scoped: they may upload bookmarks and read tags and categories, nothing
    // else. The extension only calls endpoints on that allow-list, so this firing means the
    // server's allow-list and this client have drifted apart.
    throw new ApiError(403, 'This key is limited to syncing and cannot perform that action.')
  }
  if (res.status === 429) {
    const retryAfter = res.headers.get('Retry-After')
    throw new ApiError(
      429,
      retryAfter
        ? `Too many syncs — try again in ${retryAfter} seconds.`
        : 'Too many syncs — try again shortly.',
    )
  }

  if (res.status === 405) {
    // POSTs that reach the SPA's nginx instead of the API come back 405.
    throw new ApiError(405, NOT_THE_API, true)
  }

  throw new ApiError(res.status, await errorMessage(res))
}

/**
 * The most useful text in an error body. The API answers in three shapes: `{ message }` from its
 * own handlers, a ValidationProblem (`{ title, errors: { field: [..] } }`) from request validation -
 * which is what an oversized or malformed batch gets - and plain ProblemDetails (`title`/`detail`)
 * from binding failures.
 */
async function errorMessage(res: Response): Promise<string> {
  try {
    const body: unknown = await res.json()
    if (body && typeof body === 'object') {
      const b = body as {
        message?: unknown
        errors?: unknown
        detail?: unknown
        title?: unknown
      }
      if (typeof b.message === 'string') return b.message
      if (b.errors && typeof b.errors === 'object') {
        const first = Object.values(b.errors as Record<string, unknown>)
          .flat()
          .find((m): m is string => typeof m === 'string')
        if (first) return first
      }
      if (typeof b.detail === 'string') return b.detail
      if (typeof b.title === 'string') return b.title
    }
  } catch {
    /* non-JSON error body */
  }
  return `Request failed (${res.status})`
}

/** Uploads a batch of bookmarks and returns the API's result. */
export async function postBatch(
  serverUrl: string,
  apiKey: string,
  bookmarks: BatchBookmarkItem[],
): Promise<BatchResult> {
  const res = await request('POST', serverUrl, '/bookmarks/batch', apiKey, {
    bookmarks,
    defaultTags: [],
    skipDuplicates: true,
  })
  return (await res.json()) as BatchResult
}

const NOT_THE_API =
  'That URL serves the web app, not the API — the Server URL must end in /api.'

/** Verifies the server URL and API key with a cheap authenticated read. */
export async function testConnection(serverUrl: string, apiKey: string): Promise<void> {
  const res = await request('GET', serverUrl, '/tags', apiKey)
  // Behind the gateway, anything outside /api is the SPA, whose fallback answers every GET with
  // index.html and a 200 - so a URL missing /api would "pass" here and fail later with a 405.
  if (!(res.headers.get('Content-Type') ?? '').includes('json')) {
    throw new ApiError(res.status, NOT_THE_API, true)
  }
}
