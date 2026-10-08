// Typed fetch wrapper shared by every feature's API client (SpaceManagement, Employees, ...).
// Takes the OIDC access token as a parameter rather than reaching into auth itself, so this
// module has no dependency on react-oidc-context and stays trivially testable.

import i18n, { currentLanguage } from '@/i18n'
import { API_BASE_URL as BASE_URL } from './baseUrl'

export interface ValidationErrorInfo {
  message: string
  members?: string[]
}

/** Shown in place of ABP's "Authorization failed! Given policy has not granted." */
export const permissionDeniedMessage = () => i18n.t('Error:PermissionDenied')

/** The request never got an answer: no network, the backend down or unreachable, a blocked
 * (CORS or certificate) response. Not a sign-in problem — that arrives as a 401. */
export const networkErrorMessage = () => i18n.t('Error:Network')

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
  /** No response at all (status 0) — see networkErrorMessage. */
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
          ? `${networkErrorMessage()} (${BASE_URL})`
          : networkErrorMessage()
        : permissionDenied
          ? permissionDeniedMessage()
          : (errorInfo?.message ?? i18n.t('Error:RequestFailed', { status })),
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
  return send<T>(path, token, init, false, readJson)
}

/**
 * A call that needs no sign-in (the public answer page behind a guest's link): no token is
 * sent, and a 401 is just an error — it never signs anyone out.
 */
export function requestAnonymous<T>(path: string, init?: RequestInit): Promise<T> {
  return send<T>(path, null, init, false, readJson)
}

/** A file the API answers with (a picture, say), or null when it answers 204: there is none. */
export function requestBlob(path: string, token: string, init?: RequestInit): Promise<Blob | null> {
  return send(path, token, init, false, readBlob)
}

type ReadBody<T> = (response: Response) => Promise<T>

async function readJson<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

async function readBlob(response: Response): Promise<Blob | null> {
  if (response.status === 204) return null
  // An empty body is no file either — shown as a picture it would be a broken image.
  const blob = await response.blob()
  return blob.size === 0 ? null : blob
}

async function send<T>(path: string, token: string | null, init: RequestInit | undefined, retried: boolean, read: ReadBody<T>): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: {
        // A file upload (FormData) leaves it to the browser, which adds the part boundary.
        ...(init?.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
        // ABP answers in this language: validation and business-rule messages, and later
        // the names of buildings, floors and spaces.
        'Accept-Language': currentLanguage(),
        ...(token === null ? {} : { Authorization: `Bearer ${token}` }),
        ...init?.headers,
      },
    })
  } catch (err) {
    // A cancelled request is the caller's doing, not a failure to report.
    if (err instanceof DOMException && err.name === 'AbortError') throw err
    // fetch only rejects when nothing came back ("Failed to fetch"): say what that means.
    throw new ApiError(0, null)
  }

  if (response.status === 401 && token !== null) {
    // An expired token is the usual reason. Try once to renew it quietly and repeat the
    // request; only when that fails (or the fresh token is refused too) is the session gone.
    if (!retried) {
      const fresh = await refreshToken?.().catch(() => null)
      if (fresh && fresh !== token) return send<T>(path, fresh, init, true, read)
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

  return read(response)
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
