// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ClosuresList } from './ClosuresList'
import { OverrideEffect, OverrideScope, ReasonCategory } from '@/features/space-management/api/spaceManagementApi'
import type { AvailabilityOverrideDto } from '@/features/space-management/api/spaceManagementApi'

const closure: AvailabilityOverrideDto = {
  id: 'o1',
  scope: OverrideScope.Building,
  scopeId: 'b1',
  startsAt: '2027-01-10T00:00:00',
  endsAt: '2027-01-11T00:00:00',
  effect: OverrideEffect.Closed,
  reasonCategory: ReasonCategory.Maintenance,
  reasonDetail: null,
}

function renderList(canCreate: boolean, canDelete: boolean) {
  render(
    <ClosuresList
      scope={OverrideScope.Building}
      scopeId="b1"
      ownOverrides={[closure]}
      ancestorOverrides={[]}
      isCurrentlyClosed={false}
      onCreate={vi.fn()}
      onDelete={vi.fn()}
      canCreate={canCreate}
      canDelete={canDelete}
    />,
  )
}

describe('ClosuresList permissions', () => {
  it('offers adding and deleting closures to someone allowed both', () => {
    renderList(true, true)

    expect(screen.getByRole('button', { name: '+ Add closure' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete closure' })).toBeInTheDocument()
  })

  it('still lists the closures, read-only, without Overrides.Create or Overrides.Delete', () => {
    renderList(false, false)

    expect(screen.getByText(/Maintenance/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Add closure' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete closure' })).not.toBeInTheDocument()
  })

  it('lets someone add without being able to delete', () => {
    renderList(true, false)

    expect(screen.getByRole('button', { name: '+ Add closure' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete closure' })).not.toBeInTheDocument()
  })
})
