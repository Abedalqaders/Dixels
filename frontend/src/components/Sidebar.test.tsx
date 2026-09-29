// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
  it('hides Find a space from someone who may create bookings but not view them (the page reads first)', () => {
    renderAs(granted(Permissions.Bookings.Create))

    expect(link('Find a space')).not.toBeInTheDocument()
    expect(link('My calendar')).not.toBeInTheDocument()
  })

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

describe('Sidebar drawer (below lg)', () => {
  const menuButton = () => screen.getByRole('button', { name: /menu/ })

  it('says whether it is open, and which element it controls', async () => {
    renderAs(granted(...BOOKER))

    expect(menuButton()).toHaveAttribute('aria-expanded', 'false')
    expect(menuButton()).toHaveAttribute('aria-controls', 'app-sidebar')
    await userEvent.click(menuButton())
    expect(menuButton()).toHaveAttribute('aria-expanded', 'true')
    expect(menuButton()).toHaveAccessibleName('Close menu')
  })

  it('moves focus into the drawer, and Escape closes it and gives focus back to the button', async () => {
    renderAs(granted(...BOOKER))

    await userEvent.click(menuButton())
    expect(link('My calendar')).toHaveFocus()

    await userEvent.keyboard('{Escape}')
    expect(menuButton()).toHaveAttribute('aria-expanded', 'false')
    expect(menuButton()).toHaveFocus()
  })

  it('stops the page behind it from scrolling, and lets it scroll again once closed', async () => {
    renderAs(granted(...BOOKER))

    await userEvent.click(menuButton())
    expect(document.body.style.overflow).toBe('hidden')
    await userEvent.keyboard('{Escape}')
    expect(document.body.style.overflow).toBe('')
  })

  it('closes when a page is picked from it', async () => {
    renderAs(granted(...BOOKER))

    await userEvent.click(menuButton())
    await userEvent.click(link('Find a space')!)
    expect(menuButton()).toHaveAttribute('aria-expanded', 'false')
  })

  it('gives the sign-out button a name', () => {
    renderAs(granted(...BOOKER))
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
  })
})
