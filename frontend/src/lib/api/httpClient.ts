// Typed fetch wrapper shared by every feature's API client (SpaceManagement, Employees, ...).
// Takes the OIDC access token as a parameter rather than reaching into auth itself, so this
// module has no dependency on react-oidc-context and stays trivially testable.

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:44334'

export interface ValidationErrorInfo {
  message: string
  members?: string[]
}

/** Parses ABP's structured error body (RemoteServiceErrorInfo) so callers can show the
 * backend's actual message/code instead of a generic "request failed". */
export class ApiError extends Error {
  status: number
  code?: string
  details?: string
  data?: Record<string, unknown>
  validationErrors?: ValidationErrorInfo[]

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

    super(errorInfo?.message ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = errorInfo?.code
    this.details = errorInfo?.details
    this.data = errorInfo?.data
    this.validationErrors = errorInfo?.validationErrors
  }
}

export async function request<T>(path: string, token: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...init?.headers,
    },
  })

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
