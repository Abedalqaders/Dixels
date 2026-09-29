// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from './Sidebar'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'
import { granted, WithPermissions } from '@/test/permissions'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

const BOOKER = [Permissions.Bookings.Default, Permissions.Bookings.Create]
const ADMIN = [Permissions.Buildings.Default, Permissions.SpaceTypes.Default, Permissions.Identity.Users]

function renderAs(permissions: PermissionsValue, role = 'employee') {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { role, preferred_username: 'someone' } } } as unknown as ReturnType<
    typeof useAuth
  >)
  render(
    <WithPermissions value={permissions}>
      <MemoryRouter>
        <Sidebar />
      </MemoryRouter>
    </WithPermissions>,
  )
}

const link = (name: string) => screen.queryByRole('link', { name })

describe('Sidebar', () => {
  it('splits someone who can both book and administer into Bookings and Administration', () => {
    renderAs(granted(...BOOKER, ...ADMIN), 'admin')

    expect(screen.getByText('Bookings')).toBeInTheDocument()
    expect(screen.getByText('Administration')).toBeInTheDocument()
    expect(link('Find a space')).toBeInTheDocument()
    expect(link('Hierarchy')).toBeInTheDocument()
    expect(link('Space types')).toBeInTheDocument()
    expect(link('Users')).toBeInTheDocument()
  })

  it("keeps an employee's menu a plain list, without section labels", () => {
    renderAs(granted(...BOOKER))

    expect(link('My calendar')).toBeInTheDocument()
    expect(screen.queryByText('Bookings')).not.toBeInTheDocument()
    expect(screen.queryByText('Administration')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Space management/ })).not.toBeInTheDocument()
  })

  it('drops Space types once its permission is taken away, keeping the rest', () => {
    renderAs(granted(...BOOKER, Permissions.Buildings.Default, Permissions.Identity.Users), 'admin')

    expect(link('Space types')).not.toBeInTheDocument()
    expect(link('Hierarchy')).toBeInTheDocument()
    expect(link('Users')).toBeInTheDocument()
  })

  it('drops the whole Space management group when neither of its pages is allowed', () => {
    renderAs(granted(...BOOKER, Permissions.Identity.Users), 'admin')

    expect(screen.queryByRole('button', { name: /Space management/ })).not.toBeInTheDocument()
    expect(link('Users')).toBeInTheDocument()
  })

  it('follows the grants, not the role: an admin with only booking rights sees only bookings', () => {
    renderAs(granted(...BOOKER), 'admin')

    expect(link('Find a space')).toBeInTheDocument()
    expect(screen.queryByText('Administration')).not.toBeInTheDocument()
    expect(link('Users')).not.toBeInTheDocument()
  })

  it('gives a floor editor the Hierarchy, with no building permissions at all', () => {
    renderAs(granted(Permissions.Floors.Default, Permissions.Floors.Edit))

    expect(screen.getByRole('button', { name: /Space management/ })).toBeInTheDocument()
    expect(link('Hierarchy')).toBeInTheDocument()
    expect(link('Space types')).not.toBeInTheDocument()
    expect(link('Users')).not.toBeInTheDocument()
  })

  it('hides Find a space from someone who may only view their bookings', () => {
    renderAs(granted(Permissions.Bookings.Default))

    expect(link('My calendar')).toBeInTheDocument()
    expect(link('Find a space')).not.toBeInTheDocument()
  })
})
