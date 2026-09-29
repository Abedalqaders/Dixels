// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RowActionsMenu } from './RowActionsMenu'
import type { RowMenuAction } from './RowActionsMenu'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'
import { granted, WithPermissions } from '@/test/permissions'

const actions: RowMenuAction[] = [
  { label: 'Edit details', permission: Permissions.Buildings.Edit, icon: null, onClick: vi.fn() },
  {
    label: 'Edit constraints',
    permission: [Permissions.Buildings.Edit, Permissions.Overrides.Create],
    icon: null,
    onClick: vi.fn(),
  },
  { label: 'Delete', permission: Permissions.Buildings.Delete, icon: null, onClick: vi.fn(), destructive: true },
]

function renderAs(permissions: PermissionsValue) {
  render(
    <WithPermissions value={permissions}>
      <RowActionsMenu label="HQ" actions={actions} />
    </WithPermissions>,
  )
}

describe('RowActionsMenu', () => {
  it('offers only the actions the user holds the permission for', async () => {
    renderAs(granted(Permissions.Buildings.Default, Permissions.Buildings.Delete))

    await userEvent.click(screen.getByRole('button', { name: 'HQ actions' }))

    expect(screen.getByRole('menuitem', { name: 'Delete' })).toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'Edit details' })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'Edit constraints' })).not.toBeInTheDocument()
  })

  it('takes any one of an action’s permissions as enough', async () => {
    renderAs(granted(Permissions.Overrides.Create))

    await userEvent.click(screen.getByRole('button', { name: 'HQ actions' }))

    expect(screen.getByRole('menuitem', { name: 'Edit constraints' })).toBeInTheDocument()
  })

  it('shows no menu button at all when none of the actions are allowed', () => {
    renderAs(granted(Permissions.Buildings.Default))

    expect(screen.queryByRole('button', { name: 'HQ actions' })).not.toBeInTheDocument()
  })
})
