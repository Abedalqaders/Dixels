// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { RequireAdmin } from './RequireAdmin'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

function renderAsRole(role: string) {
  vi.mocked(useAuth).mockReturnValue({
    isLoading: false,
    isAuthenticated: true,
    activeNavigator: undefined,
    error: undefined,
    user: { profile: { role } },
    signinRedirect: vi.fn(),
  } as unknown as ReturnType<typeof useAuth>)

  return render(
    <MemoryRouter initialEntries={['/admin/buildings']}>
      <Routes>
        <Route
          path="/admin/buildings"
          element={
            <RequireAdmin>
              <p>Admin content</p>
            </RequireAdmin>
          }
        />
        <Route path="/dashboard" element={<p>Employee dashboard</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAdmin', () => {
  it('redirects a signed-in non-admin to the employee landing page', () => {
    renderAsRole('employee')

    expect(screen.getByText('Employee dashboard')).toBeInTheDocument()
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument()
  })

  it('renders the wrapped route for an admin', () => {
    renderAsRole('admin')

    expect(screen.getByText('Admin content')).toBeInTheDocument()
  })
})
