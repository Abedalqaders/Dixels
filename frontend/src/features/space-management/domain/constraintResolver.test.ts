// Hand-written port of ConstraintResolverTests.cs's scenarios — no shared JSON fixture
// exists for this rule (only operating-days/operating-window do), so this can't be
// fixture-driven the way the other two domain tests are.

import { describe, expect, it } from 'vitest'
import { OperatingDays } from './operatingDays'
import { OperatingWindow } from './operatingWindow'
import {
  ConstraintNarrowingError,
  DixelsDomainErrorCodes,
  ensureDaysNarrowing,
  ensureHoursNarrowing,
  findNarrowingConflicts,
  resolve,
} from './constraintResolver'
import type { BuildingConstraintInput } from './constraintResolver'

function createBuilding(overrides: Partial<BuildingConstraintInput> = {}): BuildingConstraintInput {
  return {
    timezone: 'Asia/Amman',
    days: OperatingDays.Everyday,
    hours: OperatingWindow.FullDay,
    maxDurationMinutes: 480,
    maxHorizonDays: 14,
    minLeadMinutes: 15,
    ...overrides,
  }
}

describe('resolve', () => {
  it('falls back to building when nothing overrides', () => {
    const resolved = resolve(createBuilding(), {})

    expect(resolved.days.source).toBe('Building')
    expect(resolved.hours.source).toBe('Building')
    expect(resolved.maxDurationMinutes.source).toBe('Building')
  })

  it('lets a space narrow hours alone while keeping the building days', () => {
    const building = createBuilding()
    const spaceHours = new OperatingWindow('08:00', '18:00')

    const resolved = resolve(building, {}, { hours: spaceHours })

    expect(resolved.hours.source).toBe('Space')
    expect(resolved.hours.value.open).toBe('08:00')
    expect(resolved.days.source).toBe('Building')
  })

  it('prefers floor over building when space has no override', () => {
    const building = createBuilding()

    const resolved = resolve(building, { maxDurationMinutes: 120 }, {})

    expect(resolved.maxDurationMinutes.source).toBe('Floor')
    expect(resolved.maxDurationMinutes.value).toBe(120)
  })
})

describe('ensureHoursNarrowing', () => {
  it('throws when the child would widen access', () => {
    const buildingHours = new OperatingWindow('07:00', '20:00')
    const tooWide = new OperatingWindow('06:00', '21:00')

    expect(() => ensureHoursNarrowing(tooWide, buildingHours)).toThrow(ConstraintNarrowingError)
    try {
      ensureHoursNarrowing(tooWide, buildingHours)
    } catch (error) {
      expect((error as ConstraintNarrowingError).code).toBe(DixelsDomainErrorCodes.HoursNotNarrower)
    }
  })
})

describe('ensureDaysNarrowing', () => {
  it('allows an undefined override since undefined means inherit', () => {
    const buildingDays = OperatingDays.fromDayNames(['Monday'])

    expect(() => ensureDaysNarrowing(undefined, buildingDays)).not.toThrow()
  })
})

describe('findNarrowingConflicts', () => {
  it('flags only candidates that no longer fit', () => {
    const stillFits = { displayName: 'Meeting Room 3C', ownHours: new OperatingWindow('09:00', '17:00') }
    const noLongerFits = { displayName: 'Meeting Room 3B', ownHours: new OperatingWindow('06:00', '21:00') }
    const proposedHours = new OperatingWindow('07:00', '20:00')

    const conflicts = findNarrowingConflicts([stillFits, noLongerFits], undefined, proposedHours)

    expect(conflicts).toHaveLength(1)
    expect(conflicts[0]).toContain('Meeting Room 3B')
  })

  it('ignores candidates that inherit the field being tightened', () => {
    const inheritsHours = { displayName: 'Focus Pod 2-04' }
    const proposedHours = new OperatingWindow('07:00', '20:00')

    const conflicts = findNarrowingConflicts([inheritsHours], undefined, proposedHours)

    expect(conflicts).toHaveLength(0)
  })
})
