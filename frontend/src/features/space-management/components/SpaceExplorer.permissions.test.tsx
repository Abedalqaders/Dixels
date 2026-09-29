// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SpaceExplorer } from './SpaceExplorer'
import { getBuildings, getFloors } from '@/features/space-management/api/spaceManagementApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'
import { granted, WithPermissions } from '@/test/permissions'
import { stubMatchMedia } from '@/test/matchMedia'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()),
  getBuildings: vi.fn(),
  getFloors: vi.fn(),
  getBuilding: vi.fn(),
}))

function renderExplorer(permissions: PermissionsValue) {
  render(
    <WithPermissions value={permissions}>
      <MemoryRouter initialEntries={['/admin/buildings']}>
        <SpaceExplorer />
      </MemoryRouter>
    </WithPermissions>,
  )
}

describe('SpaceExplorer permissions', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't' } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getBuildings).mockResolvedValue({ items: [{ id: 'b1', name: 'HQ' }, { id: 'b2', name: 'Annex' }], totalCount: 2 } as never)
    vi.mocked(getFloors).mockReset()
    vi.mocked(getFloors).mockResolvedValue({ items: [{ id: 'f1', name: 'Level 1', buildingId: 'b1' }], totalCount: 1 } as never)
  })

  it('lists buildings flat for someone who may only see buildings, and never asks for floors', async () => {
    const user = userEvent.setup()
    renderExplorer(granted(Permissions.Buildings.Default))

    // The name is there, but not as a link: its floors page would only refuse them.
    expect(await screen.findByText('HQ')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'HQ' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Expand HQ' })).not.toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'Find a building' }), 'Lev')
    // The search ran (against buildings) — and looked no further.
    await vi.waitFor(() => expect(getBuildings).toHaveBeenCalledWith('t', expect.objectContaining({ filter: 'Lev' })))
    expect(getFloors).not.toHaveBeenCalled()
    expect(screen.queryByText('Floors')).not.toBeInTheDocument()
  })

  it('unfolds buildings into floors for a floor viewer', async () => {
    const user = userEvent.setup()
    renderExplorer(granted(Permissions.Floors.Default))

    expect(await screen.findByRole('link', { name: 'HQ' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Expand HQ' }))

    expect(await screen.findByRole('link', { name: 'Level 1' })).toBeInTheDocument()
    expect(getFloors).toHaveBeenCalledWith('t', expect.objectContaining({ buildingId: 'b1' }))
  })
})

describe('SpaceExplorer on small screens', () => {
  let restore: () => void = () => {}

  beforeEach(() => {
    localStorage.clear()
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't' } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getBuildings).mockResolvedValue({ items: [{ id: 'b1', name: 'HQ' }], totalCount: 1 } as never)
  })

  afterEach(() => restore())

  it('starts folded on a phone, so the list is what shows first', () => {
    restore = stubMatchMedia(390)
    renderExplorer(granted(Permissions.Buildings.Default))

    expect(screen.getByRole('button', { name: 'Show buildings panel' })).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'Find a building' })).not.toBeInTheDocument()
  })

  it('starts open on a tablet, beside the list', async () => {
    restore = stubMatchMedia(900)
    renderExplorer(granted(Permissions.Buildings.Default))

    expect(await screen.findByText('HQ')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Show buildings panel' })).not.toBeInTheDocument()
  })

  it("keeps the admin's own choice over the default for the screen", async () => {
    restore = stubMatchMedia(390)
    renderExplorer(granted(Permissions.Buildings.Default))

    await userEvent.click(screen.getByRole('button', { name: 'Show buildings panel' }))
    expect(await screen.findByText('HQ')).toBeInTheDocument()
    expect(localStorage.getItem('dixels.explorer.collapsed')).toBe('0')
  })
})
