// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { useAuth } from 'react-oidc-context'
import { PermissionsProvider } from './PermissionsProvider'
import { getGrantedPolicies } from './permissionsApi'
import { ApiError } from '@/lib/api/httpClient'
import { usePermission, usePermissions } from './usePermission'
import { Permissions } from './permissionNames'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('./permissionsApi', () => ({ getGrantedPolicies: vi.fn() }))

function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state })
  document.dispatchEvent(new Event('visibilitychange'))
}

function signedInAs(sub: string, token = `token-${sub}`) {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { sub }, access_token: token } } as unknown as ReturnType<typeof useAuth>)
}

function Probe() {
  const { status } = usePermissions()
  const canCreate = usePermission(Permissions.Bookings.Create)
  return <p>{`${status}:${canCreate ? 'can' : 'cannot'}`}</p>
}

const renderProbe = () =>
  render(
    <PermissionsProvider>
      <Probe />
    </PermissionsProvider>,
  )

describe('PermissionsProvider', () => {
  beforeEach(() => {
    vi.mocked(getGrantedPolicies).mockReset()
  })

  it("loads the signed-in user's grants with their token", async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockResolvedValue({ [Permissions.Bookings.Create]: true })
    renderProbe()

    expect(screen.getByText('loading:cannot')).toBeInTheDocument()
    expect(await screen.findByText('success:can')).toBeInTheDocument()
    expect(getGrantedPolicies).toHaveBeenCalledWith('token-u1')
  })

  it('treats a permission ABP left out as not granted', async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockResolvedValue({ [Permissions.Bookings.Default]: true })
    renderProbe()

    expect(await screen.findByText('success:cannot')).toBeInTheDocument()
  })

  it("never shows the previous user's grants to the next one", async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockResolvedValueOnce({ [Permissions.Bookings.Create]: true })
    const { rerender } = renderProbe()
    expect(await screen.findByText('success:can')).toBeInTheDocument()

    signedInAs('u2')
    vi.mocked(getGrantedPolicies).mockReturnValueOnce(new Promise(() => {}))
    rerender(
      <PermissionsProvider>
        <Probe />
      </PermissionsProvider>,
    )

    expect(screen.getByText('loading:cannot')).toBeInTheDocument()
  })

  it('keeps the grants it has when a refresh after a token renew fails', async () => {
    signedInAs('u1', 'first')
    vi.mocked(getGrantedPolicies).mockResolvedValueOnce({ [Permissions.Bookings.Create]: true })
    const { rerender } = renderProbe()
    expect(await screen.findByText('success:can')).toBeInTheDocument()

    signedInAs('u1', 'renewed')
    vi.mocked(getGrantedPolicies).mockRejectedValueOnce(new Error('Network down'))
    rerender(
      <PermissionsProvider>
        <Probe />
      </PermissionsProvider>,
    )

    await vi.waitFor(() => expect(getGrantedPolicies).toHaveBeenCalledWith('renewed'))
    expect(screen.getByText('success:can')).toBeInTheDocument()
  })

  it('picks up a grant changed in ABP when the tab is looked at again, without a loading flash', async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockResolvedValueOnce({ [Permissions.Bookings.Create]: true })
    renderProbe()
    expect(await screen.findByText('success:can')).toBeInTheDocument()

    // Create was revoked in another tab; this one comes back into view.
    vi.mocked(getGrantedPolicies).mockResolvedValueOnce({ [Permissions.Bookings.Default]: true })
    setVisibility('hidden')
    setVisibility('visible')

    expect(screen.getByText('success:can')).toBeInTheDocument() // still the old grants while the new ones load
    expect(await screen.findByText('success:cannot')).toBeInTheDocument()
    expect(getGrantedPolicies).toHaveBeenCalledTimes(2)
  })

  it("stays quiet on a 401 — the session is gone and sign-in takes over, so no 'couldn't check' card", async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockRejectedValue(new ApiError(401, null))
    renderProbe()

    await vi.waitFor(() => expect(getGrantedPolicies).toHaveBeenCalled())
    await new Promise((r) => setTimeout(r, 0))
    expect(screen.getByText('loading:cannot')).toBeInTheDocument()
    expect(screen.queryByText(/^error/)).not.toBeInTheDocument()
  })

  it('reports an error when the first load fails', async () => {
    signedInAs('u1')
    vi.mocked(getGrantedPolicies).mockRejectedValue(new Error('Network down'))
    renderProbe()

    expect(await screen.findByText('error:cannot')).toBeInTheDocument()
  })
})
