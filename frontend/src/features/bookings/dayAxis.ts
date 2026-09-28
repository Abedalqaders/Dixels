import type { DayRangeDto } from '@/features/bookings/api/bookingsApi'

export interface DayAxis {
  /** Minutes from local midnight where the bar starts and ends — shared by every row so they line up. */
  from: number
  to: number
}

/**
 * One axis for the whole result list: from the earliest opening to the latest closing
 * across the rooms (widened to include the searched window), rounded out to whole hours.
 * A 24h building gets the full day.
 */
export function dayAxis(rooms: { open: DayRangeDto[] }[], selection: { startMinute: number; endMinute: number }): DayAxis {
  const starts = rooms.flatMap((r) => r.open.map((o) => o.startMinute))
  const ends = rooms.flatMap((r) => r.open.map((o) => o.endMinute))

  const from = Math.min(selection.startMinute, ...(starts.length ? starts : [8 * 60]))
  const to = Math.max(selection.endMinute, ...(ends.length ? ends : [18 * 60]))

  return { from: Math.floor(from / 60) * 60, to: Math.min(Math.ceil(to / 60) * 60, 24 * 60) }
}
