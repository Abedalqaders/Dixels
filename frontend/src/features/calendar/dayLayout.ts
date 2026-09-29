import { dateOf, timeOf, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { OperatingWindowDto } from '@/features/space-management/api/spaceManagementApi'
import { weekday } from '@/features/calendar/calendarDates'

/** Anything with a start and end on the building's wall clock — a CalendarItem, or a test stub. */
type Timed = { localStart: string; localEnd: string }

export interface Placed<T> {
  item: T
  /** Minutes from local midnight on the day being drawn (0–1440). */
  start: number
  end: number
  /** Which side-by-side column this item sits in, and how many its overlap group needs. */
  lane: number
  lanes: number
}

/**
 * Lays out one day's items so overlapping ones sit side by side instead of on top of
 * each other. Items that overlap — directly or through a chain — form a group; each gets
 * the first free lane in its group, and the whole group shares the same lane count so
 * the columns line up. Touching items (10–11 and 11–12) don't overlap.
 */
export function layoutDay<T>(items: { item: T; start: number; end: number }[]): Placed<T>[] {
  const sorted = [...items].sort((a, b) => a.start - b.start || b.end - a.end)
  const placed: Placed<T>[] = []

  let group: Placed<T>[] = []
  let groupEnd = -1
  const closeGroup = () => {
    const lanes = Math.max(1, ...group.map((p) => p.lane + 1))
    for (const p of group) p.lanes = lanes
    group = []
  }

  for (const { item, start, end } of sorted) {
    if (start >= groupEnd && group.length > 0) closeGroup()

    // First lane whose last item has ended by now.
    let lane = 0
    while (group.some((p) => p.lane === lane && p.end > start)) lane++

    const p = { item, start, end, lane, lanes: 1 }
    group.push(p)
    placed.push(p)
    groupEnd = Math.max(groupEnd, end)
  }
  if (group.length > 0) closeGroup()

  return placed
}

/** An item's minutes on `date` — clipped at midnight, so one ending at 24:00 still draws to the bottom. */
export function itemMinutes(item: Timed, date: IsoDate): { start: number; end: number } {
  const start = dateOf(item.localStart) === date ? toMinutes(timeOf(item.localStart)) : 0
  const end = dateOf(item.localEnd) === date ? toMinutes(timeOf(item.localEnd)) : DAY_MINUTES
  return { start, end }
}

/**
 * The minutes of `date` the building is open, from its own days and hours — or null when
 * it's shut all day. The grid shades everything outside as closed. Hours that run past
 * midnight (close at or before open) count as open until the end of the day.
 */
export function openWindow(date: IsoDate, days: number[], hours: OperatingWindowDto): { from: number; to: number } | null {
  if (!days.includes(weekday(date))) return null
  if (hours.isOpen24Hours) return { from: 0, to: DAY_MINUTES }
  const from = toMinutes(hours.open)
  const close = toMinutes(hours.close)
  return { from, to: close <= from ? DAY_MINUTES : close }
}

// One hour of the grid, in pixels. 15 minutes = 12px — big enough to aim a drag at.
export const HOUR_PX = 48
export const DAY_MINUTES = 24 * 60

/**
 * Each day's items, for the columns. An item belongs to every day it covers part of — but
 * one ending exactly at midnight ("until closing" in a 24h room ends 00:00 next day)
 * doesn't reach into the next day at all.
 */
export function itemsByDay<T extends Timed>(items: T[], days: IsoDate[]): Map<IsoDate, T[]> {
  const byDay = new Map<IsoDate, T[]>()
  for (const d of days) {
    const midnight = `${d}T00:00:00`
    const list = items.filter((b) => b.localStart.startsWith(d) || (b.localStart < d && b.localEnd > midnight))
    if (list.length) byDay.set(d, list)
  }
  return byDay
}
