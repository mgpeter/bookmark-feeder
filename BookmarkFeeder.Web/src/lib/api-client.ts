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

/** Registered by the app so a 401 can restart the Auth0 login. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler
}

type TokenGetter = () => Promise<string | null>

// Default: no token. A request made before the bridge registers goes out unauthenticated and
// earns an honest 401, which is better than silently forging one with stale state.
let getToken: TokenGetter = async () => null

/**
 * Registered by ApiTokenBridge. Deliberately a plain module hook rather than a React import: this
 * client is used by the query functions in src/api/*, which must stay React-free and testable
 * without mounting a provider. Mirrors setUnauthorizedHandler above.
 */
export function setTokenGetter(getter: TokenGetter): void {
  getToken = getter
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
  const token = await getToken()

  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(buildUrl(path, params), {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (res.status === 401) {
    onUnauthorized?.()
    throw new ApiError(401, 'Your session has expired — signing you back in.')
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
