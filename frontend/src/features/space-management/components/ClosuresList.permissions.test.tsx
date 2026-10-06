// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import type { ComponentProps } from 'react'
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

function renderList(canCreate: boolean, canDelete: boolean, props: Partial<ComponentProps<typeof ClosuresList>> = {}) {
  render(
    <ClosuresList
      scope={OverrideScope.Building}
      scopeId="b1"
      ownOverrides={[closure]}
      ownTotalCount={1}
      page={0}
      pageSize={10}
      onPageChange={vi.fn()}
      onPageSizeChange={vi.fn()}
      showPast={false}
      onShowPastChange={vi.fn()}
      ancestorOverrides={[]}
      ancestorMore={[]}
      isCurrentlyClosed={false}
      onCreate={vi.fn()}
      onDelete={vi.fn()}
      canCreate={canCreate}
      canDelete={canDelete}
      {...props}
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

describe('ClosuresList paging', () => {
  it('asks for past closures when the box is ticked', () => {
    const onShowPastChange = vi.fn()
    renderList(false, false, { onShowPastChange })

    fireEvent.click(screen.getByRole('checkbox', { name: 'Show past closures' }))

    expect(onShowPastChange).toHaveBeenCalledWith(true)
  })

  it('says none are upcoming, not none at all, while past ones are hidden', () => {
    renderList(false, false, { ownOverrides: [], ownTotalCount: 0 })

    expect(screen.getByText('None upcoming')).toBeInTheDocument()
  })

  it('pages this level’s own closures once there are more than a page', () => {
    const onPageChange = vi.fn()
    renderList(false, false, { ownTotalCount: 25, onPageChange })

    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))

    expect(onPageChange).toHaveBeenCalledWith(1)
  })

  it('shows no pager for a single page', () => {
    renderList(false, false)

    expect(screen.queryByRole('button', { name: 'Next page' })).not.toBeInTheDocument()
  })

  it('counts the closures from a level above that are not shown', () => {
    renderList(false, false, { ancestorMore: [{ level: 'Building', count: 4 }] })

    expect(screen.getByText('+4 more from Building, on its own page')).toBeInTheDocument()
  })
})
