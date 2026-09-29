// Typed fetch wrapper shared by every feature's API client (SpaceManagement, Employees, ...).
// Takes the OIDC access token as a parameter rather than reaching into auth itself, so this
// module has no dependency on react-oidc-context and stays trivially testable.

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:44334'

// A production bundle must say where its API is; silently talking to localhost is a
// deployment mistake that would otherwise only show up as "Couldn't reach the server".
if (import.meta.env.PROD && !import.meta.env.VITE_API_BASE_URL) {
  throw new Error('VITE_API_BASE_URL must be set for a production build (see .env.example).')
}

export interface ValidationErrorInfo {
  message: string
  members?: string[]
}

/** Shown in place of ABP's "Authorization failed! Given policy has not granted." */
export const PERMISSION_DENIED_MESSAGE = "You don't have permission to do this. Ask an administrator if you think you should."

/** The request never got an answer: no network, the backend down or unreachable, a blocked
 * (CORS or certificate) response. Not a sign-in problem — that arrives as a 401. */
export const NETWORK_ERROR_MESSAGE = "Couldn't reach the server. Check your connection, or that the backend is running, and try again."

/** Every ABP authorization failure's error code starts with this (Volo.Authorization:010001–5). */
const ABP_AUTHORIZATION_CODE_PREFIX = 'Volo.Authorization:'

/** Parses ABP's structured error body (RemoteServiceErrorInfo) so callers can show the
 * backend's actual message/code instead of a generic "request failed". */
export class ApiError extends Error {
  status: number
  code?: string
  details?: string
  data?: Record<string, unknown>
  validationErrors?: ValidationErrorInfo[]
  /** The signed-in user lacks the permission this call needs. Not every 403: ABP answers a
   * broken business rule (a BusinessException) with 403 too, and that message must stay. */
  permissionDenied: boolean
  /** No response at all (status 0) — see NETWORK_ERROR_MESSAGE. */
  isNetworkError: boolean

  constructor(status: number, body: unknown) {
    const errorInfo = (
      body as {
        error?: {
          code?: string
          message?: string
          details?: string
          data?: Record<string, unknown>
          validationErrors?: ValidationErrorInfo[]
        }
      } | null
    )?.error

    // ABP tags its authorization failures by code; a 403 with no ABP body at all comes from
    // ASP.NET's own [Authorize] before ABP sees the request — both mean "not allowed".
    const permissionDenied =
      status === 403 && (errorInfo?.code?.startsWith(ABP_AUTHORIZATION_CODE_PREFIX) === true || errorInfo === undefined)

    const isNetworkError = status === 0
    super(
      isNetworkError
        ? import.meta.env.DEV
          ? `${NETWORK_ERROR_MESSAGE} (${BASE_URL})`
          : NETWORK_ERROR_MESSAGE
        : permissionDenied
          ? PERMISSION_DENIED_MESSAGE
          : (errorInfo?.message ?? `Request failed with status ${status}`),
    )
    this.name = 'ApiError'
    this.status = status
    this.permissionDenied = permissionDenied
    this.isNetworkError = isNetworkError
    this.code = errorInfo?.code
    this.details = errorInfo?.details
    this.data = errorInfo?.data
    this.validationErrors = errorInfo?.validationErrors
  }
}

let onUnauthorized: (() => void) | undefined
let refreshToken: (() => Promise<string | null>) | undefined

/**
 * What to do when the API answers 401 and a token renew could not rescue the request — the
 * session is gone (token revoked, account removed). The auth layer sets it once; this module
 * still knows nothing about OIDC.
 */
export function setUnauthorizedHandler(handler: (() => void) | undefined) {
  onUnauthorized = handler
}

/**
 * How to get a fresh access token when the API answers 401 — normally a silent renew
 * through the OIDC library. Resolves null when that is not possible; the request is then
 * treated as a dead session (see setUnauthorizedHandler). Set by the auth layer.
 */
export function setTokenRefresher(refresher: (() => Promise<string | null>) | undefined) {
  refreshToken = refresher
}

export function request<T>(path: string, token: string, init?: RequestInit): Promise<T> {
  return send<T>(path, token, init, false)
}

async function send<T>(path: string, token: string, init: RequestInit | undefined, retried: boolean): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
        ...init?.headers,
      },
    })
  } catch (err) {
    // A cancelled request is the caller's doing, not a failure to report.
    if (err instanceof DOMException && err.name === 'AbortError') throw err
    // fetch only rejects when nothing came back ("Failed to fetch"): say what that means.
    throw new ApiError(0, null)
  }

  if (response.status === 401) {
    // An expired token is the usual reason. Try once to renew it quietly and repeat the
    // request; only when that fails (or the fresh token is refused too) is the session gone.
    if (!retried) {
      const fresh = await refreshToken?.().catch(() => null)
      if (fresh && fresh !== token) return send<T>(path, fresh, init, true)
    }
    onUnauthorized?.()
  }

  if (!response.ok) {
    let body: unknown = null
    try {
      body = await response.json()
    } catch {
      // No JSON body to parse (e.g. a network-level or non-ABP error) — ApiError falls
      // back to a generic message in that case.
    }
    throw new ApiError(response.status, body)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function query(params: Record<string, string | number | boolean | undefined>): string {
  const usp = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) usp.set(key, String(value))
  }
  const s = usp.toString()
  return s ? `?${s}` : ''
}

export interface ListResultDto<T> {
  items: T[]
}

export interface PagedResultDto<T> extends ListResultDto<T> {
  totalCount: number
}
