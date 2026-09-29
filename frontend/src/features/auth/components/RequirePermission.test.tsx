// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { RequirePermission } from './RequirePermission'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'
import { HOME_PATH } from '@/features/auth/landing'
import { granted, WithPermissions } from '@/test/permissions'
import { ApiError } from '@/lib/api/httpClient'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

function renderWith(permissions: PermissionsValue) {
  return render(
    <WithPermissions value={permissions}>
      <MemoryRouter initialEntries={['/find-space']}>
        <Routes>
          <Route
            path="/find-space"
            element={
              <RequirePermission name={Permissions.Bookings.Create} deniedTitle="You can't book spaces">
                <p>Find a space</p>
              </RequirePermission>
            }
          />
          <Route path={HOME_PATH} element={<p>Home</p>} />
        </Routes>
      </MemoryRouter>
    </WithPermissions>,
  )
}

describe('RequirePermission', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({
      isLoading: false,
      isAuthenticated: true,
      activeNavigator: undefined,
      error: undefined,
      user: { profile: { role: 'employee' } },
      signinRedirect: vi.fn(),
    } as unknown as ReturnType<typeof useAuth>)
  })

  it('renders the page for a user holding the permission', () => {
    renderWith(granted(Permissions.Bookings.Default, Permissions.Bookings.Create))

    expect(screen.getByText('Find a space')).toBeInTheDocument()
  })

  it("tells a user without it why, in place, with a way home", async () => {
    renderWith(granted(Permissions.Bookings.Default))

    expect(screen.getByRole('alert')).toHaveTextContent("You can't book spaces")
    expect(screen.queryByText('Find a space')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('link', { name: 'Go to your home page' }))
    expect(screen.getByText('Home')).toBeInTheDocument()
  })

  it('lets any one of a list of permissions through', () => {
    render(
      <WithPermissions value={granted(Permissions.Spaces.Default)}>
        <MemoryRouter>
          <RequirePermission name={[Permissions.Floors.Default, Permissions.Spaces.Default]}>
            <p>Floors</p>
          </RequirePermission>
        </MemoryRouter>
      </WithPermissions>,
    )

    expect(screen.getByText('Floors')).toBeInTheDocument()
  })

  it("waits while the grants load instead of flashing 'no access'", () => {
    renderWith({ status: 'loading', retry: () => {} })

    expect(screen.getByRole('status', { name: 'Checking your access…' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it("says the server was unreachable when that's what happened — not that access was refused", () => {
    renderWith({ status: 'error', error: new ApiError(0, null), retry: () => {} })

    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't reach the server")
    expect(screen.getByRole('alert')).not.toHaveTextContent("Couldn't check your access")
  })

  it('offers a retry when the grants could not be loaded', async () => {
    const retry = vi.fn()
    renderWith({ status: 'error', error: new Error('Network down'), retry })

    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't check your access")
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(retry).toHaveBeenCalledOnce()
  })
})
