// Which times a room can actually be booked on one day, for the booking form's From/To lists
// and its date picker. Pure functions over minutes-from-midnight, built on the same free-time
// rules as dragging on a day bar (dragRange.ts), so the two can never disagree.

import { freeStretchAt } from './dragRange'
import type { FreeTimeRules } from './dragRange'
import type { SpaceDayDto } from './api/bookingsApi'

const DAY_MINUTES = 24 * 60

/** One day of a room as free-time rules: its open times, with bookings and closures in the way. */
export function dayRules(day: SpaceDayDto, rest: Omit<FreeTimeRules, 'open' | 'blockers'>): FreeTimeRules {
  return {
    ...rest,
    open: day.open.map((r) => ({ start: r.startMinute, end: r.endMinute })),
    blockers: [...day.busy, ...day.closed].map((r) => ({ start: r.startMinute, end: r.endMinute })),
  }
}

/** Every start time on the slot grid with at least one free slot after it. */
export function startTimes(rules: FreeTimeRules): number[] {
  const slot = rules.slotMinutes
  const starts: number[] = []
  for (let m = 0; m + slot <= DAY_MINUTES; m += slot) {
    const stretch = freeStretchAt(m, rules)
    if (stretch && stretch.end - m >= slot) starts.push(m)
  }
  return starts
}

/** Every end time for a booking from `start`: up to the next booking, closure or closing time, and the room's maximum. */
export function endTimes(start: number, rules: FreeTimeRules): number[] {
  const stretch = freeStretchAt(start, rules)
  if (!stretch) return []

  const last = Math.min(stretch.end, start + rules.maxDuration)
  const ends: number[] = []
  for (let m = start + rules.slotMinutes; m <= last; m += rules.slotMinutes) ends.push(m)
  return ends
}

export function hasFreeTime(rules: FreeTimeRules): boolean {
  return startTimes(rules).length > 0
}

/**
 * The earliest free range that's `length` long (capped at the maximum) — else the earliest
 * free range at all, shorter. Null when the day has no free time.
 */
export function firstFreeRange(length: number, rules: FreeTimeRules): { start: number; end: number } | null {
  const wanted = Math.min(length, rules.maxDuration)
  for (const s of startTimes(rules)) {
    if (endTimes(s, rules).includes(s + wanted)) return { start: s, end: s + wanted }
  }
  return nearestFreeRange(0, length, rules)
}

/** Whether `start`–`end` is one of the ranges the From/To lists offer. */
export function fitsFreeTime(start: number, end: number, rules: FreeTimeRules): boolean {
  return startTimes(rules).includes(start) && endTimes(start, rules).includes(end)
}

/**
 * The bookable range closest to a wanted one: the first free start at or after `start`
 * (else the day's first), as near `length` long as fits. Null when the day has no free time.
 */
export function nearestFreeRange(start: number, length: number, rules: FreeTimeRules): { start: number; end: number } | null {
  const starts = startTimes(rules)
  if (starts.length === 0) return null

  const s = starts.find((m) => m >= start) ?? starts[0]
  const ends = endTimes(s, rules)
  const end = ends.filter((m) => m <= s + length).pop() ?? ends[0]
  return { start: s, end }
}
