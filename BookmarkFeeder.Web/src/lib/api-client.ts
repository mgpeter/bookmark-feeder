import { getConfig } from '@/config/config-context'

export class ApiError extends Error {
  status: number
  body?: unknown

  constructor(status: number, message: string, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

type UnauthorizedHandler = () => void
let onUnauthorized: UnauthorizedHandler | null = null

/** Registered by the app so a 401 can route the user to Settings. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler
}

function buildUrl(path: string, params?: Record<string, unknown>): string {
  // The app is served from the same origin as the API (behind the gateway), so all
  // requests are relative to the current origin under the /api prefix.
  const url = new URL('/api' + path, window.location.origin)
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, String(value))
      }
    }
  }
  return url.toString()
}

interface RequestOptions {
  params?: Record<string, unknown>
  body?: unknown
}

async function request<T>(
  method: string,
  path: string,
  { params, body }: RequestOptions = {},
): Promise<T> {
  const { apiKey } = getConfig()

  const res = await fetch(buildUrl(path, params), {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-API-Key': apiKey,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (res.status === 401) {
    onUnauthorized?.()
    throw new ApiError(401, 'Unauthorized — check your API key.')
  }

  if (!res.ok) {
    let parsed: unknown
    let message = `Request failed (${res.status})`
    try {
      parsed = await res.json()
      if (parsed && typeof parsed === 'object' && 'message' in parsed) {
        message = String((parsed as { message: unknown }).message)
      }
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(res.status, message, parsed)
  }

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const api = {
  get: <T>(path: string, params?: Record<string, unknown>) =>
    request<T>('GET', path, { params }),
  // params as well as a body: bulk actions filter via the query string (the same shape the
  // GET uses) while carrying their target state in the body.
  post: <T>(path: string, body?: unknown, params?: Record<string, unknown>) =>
    request<T>('POST', path, { body, params }),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, { body }),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, { body }),
  del: <T>(path: string, params?: Record<string, unknown>) =>
    request<T>('DELETE', path, { params }),
}
