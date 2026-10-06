// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import {
  getActiveOverrides,
  getBuilding,
  getOverrides,
  OverrideEffect,
  OverrideScope,
  ReasonCategory,
} from '@/features/space-management/api/spaceManagementApi'
import type { AvailabilityOverrideDto, BuildingDto } from '@/features/space-management/api/spaceManagementApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { TestProviders } from '@/test/providers'
import { granted, WithPermissions } from '@/test/permissions'
import { AdminConstraintsPage } from './AdminConstraintsPage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()
  return { ...actual, getBuilding: vi.fn(), getOverrides: vi.fn(), getActiveOverrides: vi.fn() }
})

const building = {
  id: 'b1',
  name: 'Riverside HQ',
  names: [],
  buildingNumber: null,
  timezone: 'UTC',
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: true, open: '00:00', close: '00:00' },
  maxDurationMinutes: 240,
  maxHorizonDays: 30,
  maxSeriesHorizonDays: 90,
  minLeadMinutes: 0,
  ownOverlapPolicy: 0,
  isDeleted: false,
  concurrencyStamp: 'x',
} as unknown as BuildingDto

function closure(id: string, startsAt: Date, endsAt: Date): AvailabilityOverrideDto {
  return {
    id,
    scope: OverrideScope.Building,
    scopeId: 'b1',
    startsAt: startsAt.toISOString(),
    endsAt: endsAt.toISOString(),
    effect: OverrideEffect.Closed,
    reasonCategory: ReasonCategory.Maintenance,
    reasonDetail: null,
  }
}

const day = 24 * 60 * 60 * 1000

function renderPage() {
  const router = createMemoryRouter([{ path: '/admin/:level/:id/constraints', element: <AdminConstraintsPage /> }], {
    initialEntries: ['/admin/building/b1/constraints'],
  })
  render(
    <TestProviders>
      <WithPermissions value={granted(Permissions.Overrides.Default)}>
        <RouterProvider router={router} />
      </WithPermissions>
    </TestProviders>,
  )
}

describe('AdminConstraintsPage closures', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't', profile: {} } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getBuilding).mockResolvedValue(building)
  })

  it('reads "closed now" from what is in effect, even when the list page shows other closures', async () => {
    const now = Date.now()
    // A long closure that started months ago and is still on…
    vi.mocked(getActiveOverrides).mockResolvedValue({ items: [closure('on', new Date(now - 90 * day), new Date(now + day))] })
    // …while the first page of the list holds only the soonest upcoming ones.
    vi.mocked(getOverrides).mockResolvedValue({
      items: [closure('next', new Date(now + 2 * day), new Date(now + 3 * day))],
      totalCount: 61,
    })

    renderPage()

    expect(await screen.findByText('Currently closed')).toBeInTheDocument()
    expect(getActiveOverrides).toHaveBeenCalledWith('t', OverrideScope.Building, 'b1')
    expect(getOverrides).toHaveBeenCalledWith('t', OverrideScope.Building, 'b1', {
      includePast: false,
      skipCount: 0,
      maxResultCount: 10,
    })
  })

  it('shows open when nothing is in effect, whatever the list holds', async () => {
    const now = Date.now()
    vi.mocked(getActiveOverrides).mockResolvedValue({ items: [] })
    vi.mocked(getOverrides).mockResolvedValue({
      items: [closure('next', new Date(now + 2 * day), new Date(now + 3 * day))],
      totalCount: 1,
    })

    renderPage()

    expect(await screen.findByText('Closures')).toBeInTheDocument()
    expect(screen.queryByText('Currently closed')).not.toBeInTheDocument()
  })
})
