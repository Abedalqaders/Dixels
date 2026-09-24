// Port of backend/src/Dixels.Domain/SpaceManagement/ConstraintResolver.cs
//
// Stateless resolution of the Building -> Floor -> Space constraint hierarchy: a
// COALESCE-equivalent, per-field pick (space wins, then floor, then building), with
// provenance. The backend resolves against Building/Floor/Space entity classes; the
// frontend has no such entities yet (only mock data), so this operates on plain DTOs
// with the same field-precedence semantics instead.

import { OperatingDays } from './operatingDays'
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
