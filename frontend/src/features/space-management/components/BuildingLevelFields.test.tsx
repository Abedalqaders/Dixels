// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { OwnOverlapPolicy } from '@/features/space-management/api/spaceManagementApi'
import { OperatingDays } from '@/features/space-management/operatingDays'
import { OperatingWindow } from '@/features/space-management/operatingWindow'
import { BuildingLevelFields } from './BuildingLevelFields'
import type { BuildingDraft } from './BuildingLevelFields'
import { buildingDraftEquals } from './draftEquality'

const draft: BuildingDraft = {
  days: OperatingDays.Everyday,
  hours: OperatingWindow.FullDay,
  maxDurationMinutes: 120,
  maxHorizonDays: 60,
  maxSeriesHorizonDays: 90,
  minLeadMinutes: 15,
  ownOverlapPolicy: OwnOverlapPolicy.Warn,
}

describe('BuildingLevelFields — recurring bookings horizon', () => {
  it('moves up with the booking horizon, never below it', () => {
    const onChange = vi.fn()
    render(<BuildingLevelFields draft={draft} buildingName="Riverside HQ" onChange={onChange} />)

    fireEvent.change(screen.getByDisplayValue('60'), { target: { value: '120' } })
    expect(onChange.mock.lastCall![0]).toMatchObject({ maxHorizonDays: 120, maxSeriesHorizonDays: 120 })

    fireEvent.change(screen.getByDisplayValue('60'), { target: { value: '45' } })
    expect(onChange.mock.lastCall![0]).toMatchObject({ maxHorizonDays: 45, maxSeriesHorizonDays: 90 })
  })

  it('says why a value below the booking horizon is wrong', () => {
    render(<BuildingLevelFields draft={{ ...draft, maxSeriesHorizonDays: 30 }} buildingName="Riverside HQ" onChange={vi.fn()} />)

    expect(screen.getByText('Must be at least the booking horizon (60 days).')).toBeInTheDocument()
  })
})

describe('BuildingLevelFields — overlapping bookings per person', () => {
  it('shows the current choice with what it means', () => {
    render(<BuildingLevelFields draft={draft} buildingName="Riverside HQ" onChange={vi.fn()} />)

    expect(screen.getByLabelText('Overlapping bookings per person')).toHaveDisplayValue('Allow with a warning')
    expect(screen.getByText(/told about the clash before they book/)).toBeInTheDocument()
  })

  it('changes the draft, which then counts as unsaved', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<BuildingLevelFields draft={draft} buildingName="Riverside HQ" onChange={onChange} />)

    await user.selectOptions(screen.getByLabelText('Overlapping bookings per person'), 'One booking at a time')

    const next = onChange.mock.lastCall![0] as BuildingDraft
    expect(next.ownOverlapPolicy).toBe(OwnOverlapPolicy.Block)
    expect(buildingDraftEquals(draft, next)).toBe(false)
  })
})
