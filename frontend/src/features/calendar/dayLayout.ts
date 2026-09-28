import { dateOf, timeOf, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { BookableBuildingDto, BookingDto } from '@/features/bookings/api/bookingsApi'

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

/** A booking's minutes on `date` — clipped at midnight, so one ending at 24:00 still draws to the bottom. */
export function bookingMinutes(booking: Pick<BookingDto, 'localStart' | 'localEnd'>, date: IsoDate): { start: number; end: number } {
  const start = dateOf(booking.localStart) === date ? toMinutes(timeOf(booking.localStart)) : 0
  const end = dateOf(booking.localEnd) === date ? toMinutes(timeOf(booking.localEnd)) : 24 * 60
  return { start, end }
}

/**
 * The hours the time grid shows: from the earliest opening to the latest closing across
 * the building's rooms, widened to fit any booking outside them, in whole hours. A room
 * open 24 hours (or one whose hours run past midnight) means the whole day.
 */
export function hourSpan(
  building: Pick<BookableBuildingDto, 'floors'> | null,
  bookings: { start: number; end: number }[],
): { from: number; to: number } {
  let from = Infinity
  let to = -Infinity
  for (const floor of building?.floors ?? []) {
    for (const space of floor.spaces) {
      const { isOpen24Hours, open, close } = space.hours.value
      const o = isOpen24Hours ? 0 : toMinutes(open)
      const c = isOpen24Hours || toMinutes(close) <= toMinutes(open) ? 24 * 60 : toMinutes(close)
      from = Math.min(from, o)
      to = Math.max(to, c)
    }
  }
  for (const b of bookings) {
    from = Math.min(from, b.start)
    to = Math.max(to, b.end)
  }
  if (!Number.isFinite(from)) return { from: 8, to: 18 }
  return { from: Math.floor(from / 60), to: Math.min(24, Math.ceil(to / 60)) }
}
