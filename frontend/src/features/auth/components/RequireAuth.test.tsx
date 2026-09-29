// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { RequireAuth } from './RequireAuth'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

describe('RequireAuth', () => {
  it('sends a signed-out visitor to the login page, remembering where they were', () => {
    const signinRedirect = vi.fn().mockResolvedValue(undefined)
    vi.mocked(useAuth).mockReturnValue({
      isLoading: false,
      isAuthenticated: false,
      activeNavigator: undefined,
      error: undefined,
      signinRedirect,
    } as unknown as ReturnType<typeof useAuth>)

    render(
      <MemoryRouter initialEntries={['/my-calendar?view=week&date=2026-10-07']}>
        <Routes>
          <Route path="/my-calendar" element={<RequireAuth><p>Calendar</p></RequireAuth>} />
        </Routes>
      </MemoryRouter>,
    )

    expect(screen.queryByText('Calendar')).not.toBeInTheDocument()
    expect(signinRedirect).toHaveBeenCalledWith({ state: { returnTo: '/my-calendar?view=week&date=2026-10-07' } })
  })
})
