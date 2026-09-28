// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { OwnOverlapPolicy } from '../api/spaceManagementApi'
import { OperatingDays } from '../domain/operatingDays'
import { OperatingWindow } from '../domain/operatingWindow'
import { BuildingLevelFields } from './BuildingLevelFields'
import type { BuildingDraft } from './BuildingLevelFields'
import { buildingDraftEquals } from './draftEquality'

const draft: BuildingDraft = {
  days: OperatingDays.Everyday,
  hours: OperatingWindow.FullDay,
  maxDurationMinutes: 120,
  maxHorizonDays: 60,
  minLeadMinutes: 15,
  ownOverlapPolicy: OwnOverlapPolicy.Warn,
}

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
