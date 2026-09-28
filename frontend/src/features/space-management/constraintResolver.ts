// Port of backend/src/Dixels.Domain/SpaceManagement/ConstraintResolver.cs
//
// Stateless resolution of the Building -> Floor -> Space constraint hierarchy: a
// COALESCE-equivalent, per-field pick (space wins, then floor, then building), with
// provenance. The backend resolves against Building/Floor/Space entity classes; the
// frontend has no such entities yet (only mock data), so this operates on plain DTOs
// with the same field-precedence semantics instead.

import { OperatingDays } from './operatingDays'
import type { DayName } from './operatingDays'
import { OperatingWindow } from './operatingWindow'

export type ConstraintSource = 'Building' | 'Floor' | 'Space'

export interface FieldValue<T> {
  value: T
  source: ConstraintSource
}

export interface ResolvedConstraints {
  timezone: string
  days: FieldValue<OperatingDays>
  hours: FieldValue<OperatingWindow>
  maxDurationMinutes: FieldValue<number>
  maxHorizonDays: number
  minLeadMinutes: number
  minAttendees?: number
  capacity?: number
}

export interface BuildingConstraintInput {
  timezone: string
  days: OperatingDays
  hours: OperatingWindow
  maxDurationMinutes: number
  maxHorizonDays: number
  minLeadMinutes: number
}

export interface FloorConstraintInput {
  days?: OperatingDays
  hours?: OperatingWindow
  maxDurationMinutes?: number
}

export interface SpaceConstraintInput extends FloorConstraintInput {
  minAttendees?: number
  capacity?: number
}

export function resolve(
  building: BuildingConstraintInput,
  floor: FloorConstraintInput,
  space?: SpaceConstraintInput,
): ResolvedConstraints {
  return {
    timezone: building.timezone,
    days: pick(space?.days, floor.days, building.days),
    hours: pick(space?.hours, floor.hours, building.hours),
    maxDurationMinutes: pick(space?.maxDurationMinutes, floor.maxDurationMinutes, building.maxDurationMinutes),
    maxHorizonDays: building.maxHorizonDays,
    minLeadMinutes: building.minLeadMinutes,
    minAttendees: space?.minAttendees,
    capacity: space?.capacity,
  }
}

function pick<T>(spaceValue: T | undefined, floorValue: T | undefined, buildingValue: T): FieldValue<T> {
  if (spaceValue !== undefined) return { value: spaceValue, source: 'Space' }
  if (floorValue !== undefined) return { value: floorValue, source: 'Floor' }
  return { value: buildingValue, source: 'Building' }
}

export const DixelsDomainErrorCodes = {
  DaysNotNarrower: 'Dixels:SpaceManagement:DaysNotNarrower',
  HoursNotNarrower: 'Dixels:SpaceManagement:HoursNotNarrower',
} as const

export class ConstraintNarrowingError extends Error {
  readonly code: string
  constructor(code: string) {
    super(code)
    this.code = code
  }
}

export function ensureDaysNarrowing(childOwn: OperatingDays | undefined, resolvedParent: OperatingDays): void {
  if (childOwn !== undefined && !childOwn.isSubsetOf(resolvedParent)) {
    throw new ConstraintNarrowingError(DixelsDomainErrorCodes.DaysNotNarrower)
  }
}

export function ensureHoursNarrowing(childOwn: OperatingWindow | undefined, resolvedParent: OperatingWindow): void {
  if (childOwn !== undefined && !childOwn.isSubsetOf(resolvedParent)) {
    throw new ConstraintNarrowingError(DixelsDomainErrorCodes.HoursNotNarrower)
  }
}

export interface NarrowingCandidate {
  displayName: string
  ownDays?: OperatingDays
  ownHours?: OperatingWindow
}

/**
 * Given the descendants that have their own days/hours override set, returns the
 * display names of the ones that would no longer fit under a proposed, tighter parent
 * value. Non-blocking and informational — the save that tightens the parent still
 * proceeds (tightening never retroactively invalidates), but the admin sees exactly
 * which descendants now have a stale override.
 */
export function findNarrowingConflicts(
  candidates: NarrowingCandidate[],
  proposedDays: OperatingDays | undefined,
  proposedHours: OperatingWindow | undefined,
): string[] {
  const conflicts: string[] = []

  for (const candidate of candidates) {
    if (proposedDays !== undefined && candidate.ownDays !== undefined && !candidate.ownDays.isSubsetOf(proposedDays)) {
      conflicts.push(`${candidate.displayName} no longer fits inside the new operating days`)
    }

    if (proposedHours !== undefined && candidate.ownHours !== undefined && !candidate.ownHours.isSubsetOf(proposedHours)) {
      conflicts.push(`${candidate.displayName} no longer fits inside the new operating hours`)
    }
  }

  return conflicts
}

// isCurrentlyClosed — frontend-only display logic (no backend port exists for this: the
// backend never needs to answer "is it closed right now", only "what are the resolved
// constraints and closures", leaving the point-in-time resolution to whoever's displaying it).

export type OverrideEffect = 'Closed' | 'Open'

/** The subset of AvailabilityOverride's fields this needs — startsAt/endsAt as anything
 * `Date` can parse, matching how the API client will hand these back over JSON. */
export interface OverrideWindow {
  startsAt: string | Date
  endsAt: string | Date
  effect: OverrideEffect
}

const DAY_NAMES_BY_JS_INDEX: readonly DayName[] = [
  'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday',
]

/**
 * Resolves "is this space/floor/building closed at this instant", per the closures-union
 * rule: a Closed override at any level always wins on overlap; an Open override only
 * extends bookability beyond the resolved days/hours and is beaten by any overlapping
 * Closed override. With no overlapping override either way, falls back to the resolved
 * operating days/hours.
 *
 * `overrides` should already be the ones applicable to this node (its own plus every
 * ancestor's, per the closures-union rule) — this function doesn't know about the
 * hierarchy, only about resolving a flat list of windows against a point in time.
 */
export function isCurrentlyClosed(
  resolvedDays: OperatingDays,
  resolvedHours: OperatingWindow,
  overrides: readonly OverrideWindow[],
  at: Date,
): boolean {
  const covering = overrides.filter((override) => coversInstant(override, at))

  if (covering.some((override) => override.effect === 'Closed')) {
    return true
  }

  if (covering.some((override) => override.effect === 'Open')) {
    return false
  }

  return !isWithinOperatingWindow(resolvedDays, resolvedHours, at)
}

function coversInstant(override: OverrideWindow, at: Date): boolean {
  const startsAt = override.startsAt instanceof Date ? override.startsAt : new Date(override.startsAt)
  const endsAt = override.endsAt instanceof Date ? override.endsAt : new Date(override.endsAt)
  return at >= startsAt && at < endsAt
}

function isWithinOperatingWindow(days: OperatingDays, hours: OperatingWindow, at: Date): boolean {
  const minutesOfDay = at.getHours() * 60 + at.getMinutes()

  if (hours.isOpen24Hours) {
    return days.contains(DAY_NAMES_BY_JS_INDEX[at.getDay()])
  }

  const open = toMinutesFromHHmm(hours.open)
  const close = toMinutesFromHHmm(hours.close)
  const wraps = open > close

  if (wraps ? minutesOfDay < open && minutesOfDay >= close : minutesOfDay < open || minutesOfDay >= close) {
    return false
  }

  // A wrapping window that's still open past midnight (e.g. 22:00-02:00, checked at 01:00)
  // belongs to *yesterday's* allowed day, not today's — the session started the evening
  // before and hasn't ended yet.
  const belongsToPreviousDay = wraps && minutesOfDay < close
  const relevantDayIndex = belongsToPreviousDay ? (at.getDay() + 6) % 7 : at.getDay()

  return days.contains(DAY_NAMES_BY_JS_INDEX[relevantDayIndex])
}

function toMinutesFromHHmm(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}
