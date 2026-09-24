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
  isCurrentlyClosed,
  resolve,
} from './constraintResolver'
import type { BuildingConstraintInput, OverrideWindow } from './constraintResolver'

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

describe('isCurrentlyClosed', () => {
  const weekdays = OperatingDays.fromDayNames(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
  const nineToFive = new OperatingWindow('09:00', '17:00')
  const overnight = new OperatingWindow('22:00', '02:00')

  function at(isoLocal: string): Date {
    return new Date(isoLocal)
  }

  it('is open during resolved hours on an allowed day, with no overrides', () => {
    expect(isCurrentlyClosed(weekdays, nineToFive, [], at('2026-09-23T10:00:00'))).toBe(false) // Wednesday
  })

  it('is closed outside resolved hours on an allowed day', () => {
    expect(isCurrentlyClosed(weekdays, nineToFive, [], at('2026-09-23T20:00:00'))).toBe(true)
  })

  it('is closed on a day not in the resolved days, even during resolved hours', () => {
    expect(isCurrentlyClosed(weekdays, nineToFive, [], at('2026-09-26T10:00:00'))).toBe(true) // Saturday
  })

  it('a wrapping window just after midnight belongs to the day it started, not today', () => {
    // Sat 2026-09-26 01:00 belongs to Friday's overnight session — Friday is allowed, so
    // this is open even though Saturday itself isn't (a naive "check today's day" reading
    // would wrongly call this closed).
    expect(isCurrentlyClosed(weekdays, overnight, [], at('2026-09-26T01:00:00'))).toBe(false)

    // Mon 2026-09-21 01:00 belongs to Sunday's overnight session — Sunday isn't allowed, so
    // this is closed even though Monday itself is (the naive reading would wrongly call
    // this open).
    expect(isCurrentlyClosed(weekdays, overnight, [], at('2026-09-21T01:00:00'))).toBe(true)
  })

  it('a Closed override wins even during otherwise-open resolved hours', () => {
    const closure: OverrideWindow = { startsAt: '2026-09-23T00:00:00', endsAt: '2026-09-24T00:00:00', effect: 'Closed' }
    expect(isCurrentlyClosed(weekdays, nineToFive, [closure], at('2026-09-23T10:00:00'))).toBe(true)
  })

  it('an Open override extends bookability outside the resolved hours', () => {
    const specialOpening: OverrideWindow = { startsAt: '2026-09-26T08:00:00', endsAt: '2026-09-26T20:00:00', effect: 'Open' }
    expect(isCurrentlyClosed(weekdays, nineToFive, [specialOpening], at('2026-09-26T10:00:00'))).toBe(false) // Saturday, normally closed
  })

  it('a Closed override beats an overlapping Open override at any level', () => {
    const specialOpening: OverrideWindow = { startsAt: '2026-09-26T08:00:00', endsAt: '2026-09-26T20:00:00', effect: 'Open' }
    const floorMaintenance: OverrideWindow = { startsAt: '2026-09-26T09:00:00', endsAt: '2026-09-26T11:00:00', effect: 'Closed' }
    expect(isCurrentlyClosed(weekdays, nineToFive, [specialOpening, floorMaintenance], at('2026-09-26T10:00:00'))).toBe(true)
  })

  it('an Open override that has already ended no longer applies', () => {
    const pastOpening: OverrideWindow = { startsAt: '2026-09-26T08:00:00', endsAt: '2026-09-26T09:00:00', effect: 'Open' }
    expect(isCurrentlyClosed(weekdays, nineToFive, [pastOpening], at('2026-09-26T10:00:00'))).toBe(true)
  })
})
