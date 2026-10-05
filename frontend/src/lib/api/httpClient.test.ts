import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, networkErrorMessage, permissionDeniedMessage, request, requestBlob, setTokenRefresher, setUnauthorizedHandler } from './httpClient'

const respond = (status: number) =>
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ error: { message: 'nope' } }), { status }))

afterEach(() => {
  setUnauthorizedHandler(undefined)
  setTokenRefresher(undefined)
  vi.restoreAllMocks()
})

describe('request', () => {
  it('tells the auth layer when the session is gone (401), and still fails the call', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    respond(401)

    await expect(request('/api/app/x', 'token')).rejects.toBeInstanceOf(ApiError)
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it("leaves other failures to the page — a 403 isn't a lost session", async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    respond(403)

    await expect(request('/api/app/x', 'token')).rejects.toMatchObject({ status: 403 })
    expect(onUnauthorized).not.toHaveBeenCalled()
  })
})

describe('request without a response', () => {
  it('turns "Failed to fetch" into a message that says the server was unreachable', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))

    const error = await request('/api/app/x', 'token').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).isNetworkError).toBe(true)
    expect((error as ApiError).status).toBe(0)
    expect((error as ApiError).message).toContain(networkErrorMessage())
  })

  it("leaves a cancelled request alone — that's the caller's own doing", async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new DOMException('aborted', 'AbortError'))

    await expect(request('/api/app/x', 'token')).rejects.toMatchObject({ name: 'AbortError' })
  })
})

describe('ApiError', () => {
  it("says a missing permission in plain words instead of ABP's policy message", () => {
    const error = new ApiError(403, {
      error: { code: 'Volo.Authorization:010001', message: 'Authorization failed! Given policy has not granted.' },
    })

    expect(error.permissionDenied).toBe(true)
    expect(error.message).toBe(permissionDeniedMessage())
  })

  it('treats a bare 403 (ASP.NET refused before ABP) as a missing permission, not "status 403"', () => {
    const error = new ApiError(403, null)

    expect(error.permissionDenied).toBe(true)
    expect(error.message).toBe(permissionDeniedMessage())
  })

  it("keeps a business rule's own message — ABP sends those as 403 too", () => {
    const error = new ApiError(403, { error: { code: 'Dixels:BookingInvalidTimeRange', message: 'The end must be after the start.' } })

    expect(error.permissionDenied).toBe(false)
    expect(error.message).toBe('The end must be after the start.')
  })
})

describe('request with a token refresher (an expired token)', () => {
  it('renews once and repeats the request with the fresh token', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    setTokenRefresher(vi.fn(async () => 'fresh-token'))
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }))

    await expect(request('/api/app/x', 'stale-token')).resolves.toEqual({ ok: true })

    expect(fetchSpy).toHaveBeenCalledTimes(2)
    const retryHeaders = (fetchSpy.mock.calls[1]![1] as RequestInit).headers as Record<string, string>
    expect(retryHeaders.Authorization).toBe('Bearer fresh-token')
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it('gives up (session gone) when the renew fails, without retrying', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    setTokenRefresher(vi.fn(async () => null))
    const fetchSpy = respond(401)

    await expect(request('/api/app/x', 'stale-token')).rejects.toMatchObject({ status: 401 })

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('gives up when the fresh token is refused too — one retry, never a loop', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    const refresher = vi.fn(async () => 'fresh-token')
    setTokenRefresher(refresher)
    const fetchSpy = respond(401)

    await expect(request('/api/app/x', 'stale-token')).rejects.toMatchObject({ status: 401 })

    expect(fetchSpy).toHaveBeenCalledTimes(2)
    expect(refresher).toHaveBeenCalledOnce()
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })
})

describe('files', () => {
  it('reads a file the API answers with, and null for 204 (there is none)', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch')
    fetch.mockResolvedValueOnce(new Response(new Blob(['png-bytes'], { type: 'image/png' }), { status: 200 }))
    fetch.mockResolvedValueOnce(new Response(null, { status: 204 }))

    const picture = await requestBlob('/api/app/profile-picture', 'token')
    expect(await picture?.text()).toBe('png-bytes')
    expect(await requestBlob('/api/app/profile-picture', 'token')).toBeNull()
  })

  it('reads an empty file as no file', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(new Blob([]), { status: 200 }))

    expect(await requestBlob('/api/app/profile-picture', 'token')).toBeNull()
  })

  it('sends a file as form data, leaving its content type to the browser', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    const form = new FormData()
    form.append('file', new Blob(['x']), 'a.jpg')

    await request('/api/app/profile-picture', 'token', { method: 'PUT', body: form })

    const headers = fetch.mock.calls[0][1]!.headers as Record<string, string>
    expect(headers['Content-Type']).toBeUndefined()
    expect(headers.Authorization).toBe('Bearer token')
  })

  it('still sends JSON as JSON', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status: 200 }))

    await request('/api/app/x', 'token', { method: 'POST', body: '{}' })

    const headers = fetch.mock.calls[0][1]!.headers as Record<string, string>
    expect(headers['Content-Type']).toBe('application/json')
  })
})
