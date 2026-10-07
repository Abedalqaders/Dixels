// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from './Sidebar'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { granted, WithPermissions } from '@/test/permissions'
import { TestProviders } from '@/test/providers'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

const ADMIN = [Permissions.Buildings.Default, Permissions.SpaceTypes.Default, Permissions.Identity.Users]

function renderAt(path: string) {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { role: 'admin', preferred_username: 'someone' } } } as unknown as ReturnType<
    typeof useAuth
  >)
  render(
    <TestProviders>
      <WithPermissions value={granted(...ADMIN)}>
        <MemoryRouter initialEntries={[path]}>
          <Sidebar />
        </MemoryRouter>
      </WithPermissions>
    </TestProviders>,
  )
}

const toggle = () => screen.getByRole('button', { name: /Space management/ })
const link = (name: string) => screen.getByRole('link', { name })

describe('Sidebar: the Space management group', () => {
  it.each([
    ['/admin/buildings', 'Hierarchy'],
    ['/admin/buildings/b1/floors', 'Hierarchy'],
    ['/admin/buildings/b1/floors/f1/spaces', 'Hierarchy'],
    ['/admin/space-types', 'Space types'],
  ])('on %s it is open and highlighted, with %s as the current page', (path, current) => {
    renderAt(path)

    expect(toggle()).toHaveClass('on')
    expect(toggle()).toHaveAttribute('aria-expanded', 'true')
    expect(link(current)).toHaveClass('on')
    expect(link('Users')).not.toHaveClass('on')
  })

  it('stays folded and quiet on a page outside it', () => {
    renderAt('/admin/users')

    expect(toggle()).not.toHaveClass('on')
    expect(toggle()).toHaveAttribute('aria-expanded', 'false')
    expect(link('Users')).toHaveClass('on')
  })

  it('keeps a choice made by hand', async () => {
    renderAt('/admin/buildings')

    await userEvent.click(toggle())
    expect(toggle()).toHaveAttribute('aria-expanded', 'false')
    await userEvent.click(toggle())
    expect(toggle()).toHaveAttribute('aria-expanded', 'true')
  })
})

describe('Sidebar without its entries', () => {
  it('fails loudly instead of quietly showing no links', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() =>
      render(
        <WithPermissions value={granted(...ADMIN)}>
          <MemoryRouter>
            <Sidebar />
          </MemoryRouter>
        </WithPermissions>,
      ),
    ).toThrow(/NavContext/)
  })
})
