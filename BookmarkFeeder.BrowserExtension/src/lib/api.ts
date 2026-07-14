import type { BatchBookmarkItem, BatchResult } from './types'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

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
    // fetch only rejects on network-level failures (server down, DNS, CORS).
    throw new ApiError(0, 'Could not reach the server — check the URL and that it is running.')
  }

  if (res.ok) return res

  if (res.status === 401) {
    throw new ApiError(401, 'Unauthorized — check your API key.')
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

  throw new ApiError(res.status, await errorMessage(res))
}

async function errorMessage(res: Response): Promise<string> {
  try {
    const parsed: unknown = await res.json()
    if (parsed && typeof parsed === 'object' && 'message' in parsed) {
      return String((parsed as { message: unknown }).message)
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

/** Verifies the server URL and API key with a cheap authenticated read. */
export async function testConnection(serverUrl: string, apiKey: string): Promise<void> {
  await request('GET', serverUrl, '/tags', apiKey)
}
