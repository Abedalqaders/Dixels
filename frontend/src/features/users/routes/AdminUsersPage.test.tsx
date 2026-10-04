// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getRoleNames, getUserRoles, getUsers } from '@/features/users/api/usersApi'
import type { IdentityUserDto } from '@/features/users/api/usersApi'
import { getBuilding, getBuildings } from '@/features/space-management/api/spaceManagementApi'
import type { BuildingDto } from '@/features/space-management/api/spaceManagementApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { setLanguage } from '@/i18n'
import { TestProviders } from '@/test/providers'
import { granted, WithPermissions } from '@/test/permissions'
import { AdminUsersPage } from './AdminUsersPage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/users/api/usersApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/users/api/usersApi')>()
  return { ...actual, getUsers: vi.fn(), getUserRoles: vi.fn(), getRoleNames: vi.fn() }
})
vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()
  return { ...actual, getBuilding: vi.fn(), getBuildings: vi.fn() }
})

function user(id: string, name: string, buildingId: string | null): IdentityUserDto {
  return {
    id,
    userName: name.toLowerCase(),
    name,
    surname: null,
    email: `${name.toLowerCase()}@example.com`,
    phoneNumber: null,
    isActive: true,
    lockoutEnabled: false,
    concurrencyStamp: 'x',
    extraProperties: buildingId ? { BuildingId: buildingId } : {},
  }
}

function renderPage(grants: string[] = [Permissions.Identity.Users]) {
  render(
    <TestProviders>
      <WithPermissions value={granted(...grants)}>
        <MemoryRouter initialEntries={['/admin/users']}>
          <AdminUsersPage />
        </MemoryRouter>
      </WithPermissions>
    </TestProviders>,
  )
}

describe('AdminUsersPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't', profile: {} } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getUsers).mockResolvedValue({ items: [user('u1', 'Sara', null), user('u2', 'Omar', 'gone')], totalCount: 2 })
    vi.mocked(getUserRoles).mockResolvedValue([
      { userId: 'u1', roles: ['admin'] },
      { userId: 'u2', roles: ['employee', 'Reception'] },
    ])
    vi.mocked(getRoleNames).mockResolvedValue(['admin', 'employee', 'Reception'])
    // Omar's building has since been deleted: not found, but still in the list of deleted ones.
    vi.mocked(getBuilding).mockRejectedValue(new Error('not found'))
    vi.mocked(getBuildings).mockResolvedValue({ items: [{ id: 'gone', name: 'Old Annex' } as BuildingDto], totalCount: 1 })
  })

  it('lists who can book, with their roles and a note on a deleted building', async () => {
    renderPage()

    expect(await screen.findByText(/2 people can book\./)).toBeInTheDocument()
    // The sidebar names the signed-in user's own role too: look in the table only.
    const table = within(screen.getByRole('table'))
    expect(table.getByText('Administrator')).toBeInTheDocument()
    expect(table.getByText('Employee')).toBeInTheDocument()
    // A role an administrator made keeps its own name.
    expect(table.getByText('Reception')).toBeInTheDocument()
    expect(screen.getByText('Old Annex (deleted)')).toBeInTheDocument()
    expect(screen.getAllByText('Not assigned')).toHaveLength(2)
  })

  it('reads in Arabic, counting with the right plural form', async () => {
    await setLanguage('ar')
    renderPage()

    expect(await screen.findByText(/مستخدمان يمكنهما الحجز\./)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'المستخدمون' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'المبنى' })).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText('مسؤول')).toBeInTheDocument()
    expect(screen.getByText('Old Annex (محذوف)')).toBeInTheDocument()
  })
})
