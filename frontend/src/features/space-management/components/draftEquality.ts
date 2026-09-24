import { OperatingDays } from '../domain/operatingDays'
import { OperatingWindow } from '../domain/operatingWindow'
import type { BuildingDraft } from './BuildingLevelFields'
import type { FloorDraft } from './FloorLevelFields'
import type { SpaceDraft } from './SpaceLevelFields'

// Structural equality for the three level drafts — used by AdminConstraintsPage to decide
// whether there are unsaved changes (useUnsavedChangesWarning) without keeping a separate
// "dirty" flag that could drift out of sync with the actual draft state.

function daysEquals(a: OperatingDays, b: OperatingDays): boolean {
  return a.mask === b.mask
}

function hoursEquals(a: OperatingWindow, b: OperatingWindow): boolean {
  return a.isOpen24Hours === b.isOpen24Hours && a.open === b.open && a.close === b.close
}

function nullableEquals<T>(a: T | null, b: T | null, equals: (x: T, y: T) => boolean): boolean {
  if (a === null || b === null) return a === b
  return equals(a, b)
}

export function buildingDraftEquals(a: BuildingDraft, b: BuildingDraft): boolean {
  return (
    daysEquals(a.days, b.days) &&
    hoursEquals(a.hours, b.hours) &&
    a.maxDurationMinutes === b.maxDurationMinutes &&
    a.maxHorizonDays === b.maxHorizonDays &&
    a.minLeadMinutes === b.minLeadMinutes
  )
}

export function floorDraftEquals(a: FloorDraft, b: FloorDraft): boolean {
  return (
    nullableEquals(a.days, b.days, daysEquals) &&
    nullableEquals(a.hours, b.hours, hoursEquals) &&
    a.maxDurationMinutes === b.maxDurationMinutes
  )
}

export function spaceDraftEquals(a: SpaceDraft, b: SpaceDraft): boolean {
  return (
    nullableEquals(a.days, b.days, daysEquals) &&
    nullableEquals(a.hours, b.hours, hoursEquals) &&
    a.maxDurationMinutes === b.maxDurationMinutes &&
    a.minAttendees === b.minAttendees
  )
}
