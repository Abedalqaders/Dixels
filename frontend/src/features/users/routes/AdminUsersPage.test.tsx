// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getRoleNames, getUserPageDetails, getUsers } from '@/features/users/api/usersApi'
import type { IdentityUserDto } from '@/features/users/api/usersApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { setLanguage } from '@/i18n'
import { TestProviders } from '@/test/providers'
import { granted, WithPermissions } from '@/test/permissions'
import { AdminUsersPage } from './AdminUsersPage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/users/api/usersApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/users/api/usersApi')>()
  return { ...actual, getUsers: vi.fn(), getUserPageDetails: vi.fn(), getRoleNames: vi.fn() }
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
    // Omar's building has since been deleted: flagged, with its last name.
    vi.mocked(getUserPageDetails).mockResolvedValue([
      { userId: 'u1', buildingName: null, buildingRemoved: false, roles: ['admin'] },
      { userId: 'u2', buildingName: 'Old Annex', buildingRemoved: true, roles: ['employee', 'Reception'] },
    ])
    vi.mocked(getRoleNames).mockResolvedValue(['admin', 'employee', 'Reception'])
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
