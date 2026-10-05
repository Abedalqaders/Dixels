// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getMyProfile } from '@/features/profile/api/profileApi'
import { TestProviders } from '@/test/providers'
import { AccountMenu } from './AccountMenu'
import { initialsOf } from './UserAvatar'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

function renderMenu() {
  render(
    <TestProviders>
      <MemoryRouter initialEntries={['/my-calendar']}>
        <Routes>
          <Route path="/my-calendar" element={<AccountMenu />} />
          <Route path="/profile" element={<p>Profile page</p>} />
          <Route path="/signing-out" element={<p>Signing out</p>} />
        </Routes>
      </MemoryRouter>
    </TestProviders>,
  )
}

describe('AccountMenu', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({
      user: { access_token: 't', profile: { role: 'employee', preferred_username: 'sara' } },
    } as unknown as ReturnType<typeof useAuth>)
  })

  it("shows the token's name until the profile loads, then the profile's", async () => {
    let answer: (value: Awaited<ReturnType<typeof getMyProfile>>) => void = () => {}
    vi.mocked(getMyProfile).mockReturnValue(new Promise((resolve) => (answer = resolve)))
    renderMenu()

    expect(screen.getByRole('button', { name: 'Account: sara' })).toBeInTheDocument()

    answer({ userName: 'sara', email: 'sara@x.io', name: 'Sara', surname: 'Haddad', phoneNumber: null, concurrencyStamp: 's' })
    expect(await screen.findByRole('button', { name: 'Account: Sara Haddad' })).toHaveTextContent('SH')
  })

  it('opens My profile from the menu', async () => {
    renderMenu()

    await userEvent.click(screen.getByRole('button', { name: /^Account:/ }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'My profile' }))

    expect(screen.getByText('Profile page')).toBeInTheDocument()
  })

  it('signs out from the menu', async () => {
    renderMenu()

    await userEvent.click(screen.getByRole('button', { name: /^Account:/ }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out' }))

    expect(screen.getByText('Signing out')).toBeInTheDocument()
  })
})

describe('initialsOf', () => {
  it('takes the first letter of up to two words', () => {
    expect(initialsOf('Sara Haddad')).toBe('SH')
    expect(initialsOf('admin')).toBe('A')
    expect(initialsOf('omar.k')).toBe('OK')
  })
})
