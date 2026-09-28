import { addDays } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'

// Calendar arithmetic on "YYYY-MM-DD" strings — the building's calendar days, never the
// browser's. Weeks start on Sunday, matching the Sun–Thu work week the product targets.

export type CalendarView = 'day' | 'week' | 'month'

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

function parts(date: IsoDate) {
  const [y, m, d] = date.split('-').map(Number)
  return { y, m, d }
}

function iso(y: number, m: number, d: number): IsoDate {
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

/** True only for a real calendar date in "YYYY-MM-DD" form — "2026-13-45" or "2026-02-30" are not. */
export function isValidIsoDate(value: string | null | undefined): value is IsoDate {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const d = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(d.getTime()) && d.toISOString().slice(0, 10) === value
}

/**
 * Which month's grid holds everything a view shows: bookings are fetched (and cached) one
 * month grid at a time, and any day, any week (by the month it starts in) and any month
 * fits inside its month's grid — the grid runs from the Sunday before the 1st to the
 * Saturday after the last day.
 */
export function gridMonthFor(view: CalendarView, date: IsoDate): IsoDate {
  return startOfMonth(view === 'week' ? startOfWeek(date) : date)
}

/** 0 = Sunday … 6 = Saturday. */
export function weekday(date: IsoDate): number {
  return new Date(`${date}T00:00:00Z`).getUTCDay()
}

export function daysInMonth(date: IsoDate): number {
  const { y, m } = parts(date)
  return new Date(Date.UTC(y, m, 0)).getUTCDate()
}

export function startOfWeek(date: IsoDate): IsoDate {
  return addDays(date, -weekday(date))
}

export function startOfMonth(date: IsoDate): IsoDate {
  const { y, m } = parts(date)
  return iso(y, m, 1)
}

/** The same day `n` months on, clamped to the month's length (31 Jan + 1 month → 28/29 Feb). */
export function addMonths(date: IsoDate, n: number): IsoDate {
  const { y, m, d } = parts(date)
  const total = y * 12 + (m - 1) + n
  const first = iso(Math.floor(total / 12), (total % 12) + 1, 1)
  return iso(Math.floor(total / 12), (total % 12) + 1, Math.min(d, daysInMonth(first)))
}

/** Every day from `from` up to, not including, `to`. */
export function daysBetween(from: IsoDate, to: IsoDate): IsoDate[] {
  const days: IsoDate[] = []
  for (let d = from; d < to; d = addDays(d, 1)) days.push(d)
  return days
}

/** The month grid's first day (the Sunday on or before the 1st) and how many week rows it needs (4–6). */
export function monthGrid(date: IsoDate): { start: IsoDate; weeks: number } {
  const first = startOfMonth(date)
  return { start: startOfWeek(first), weeks: Math.ceil((weekday(first) + daysInMonth(first)) / 7) }
}

/** The days a view shows around `date`, as [from, to) — also what to fetch. */
export function visibleRange(view: CalendarView, date: IsoDate): { from: IsoDate; to: IsoDate } {
  if (view === 'day') return { from: date, to: addDays(date, 1) }
  if (view === 'week') {
    const from = startOfWeek(date)
    return { from, to: addDays(from, 7) }
  }
  const { start, weeks } = monthGrid(date)
  return { from: start, to: addDays(start, weeks * 7) }
}

/** One step back (-1) or forward (+1) in the current view. */
export function shiftDate(view: CalendarView, date: IsoDate, direction: 1 | -1): IsoDate {
  if (view === 'day') return addDays(date, direction)
  if (view === 'week') return addDays(date, 7 * direction)
  return addMonths(date, direction)
}

/** The toolbar heading: "Tuesday 29 September 2026", "27 Sep – 3 Oct 2026", "September 2026". */
export function rangeLabel(view: CalendarView, date: IsoDate): string {
  const { y, m, d } = parts(date)
  if (view === 'day') return `${WEEKDAYS[weekday(date)]} ${d} ${MONTHS[m - 1]} ${y}`
  if (view === 'month') return `${MONTHS[m - 1]} ${y}`

  const from = parts(startOfWeek(date))
  const to = parts(addDays(startOfWeek(date), 6))
  const short = (mm: number) => MONTHS[mm - 1].slice(0, 3)
  if (from.y !== to.y) return `${from.d} ${short(from.m)} ${from.y} – ${to.d} ${short(to.m)} ${to.y}`
  if (from.m !== to.m) return `${from.d} ${short(from.m)} – ${to.d} ${short(to.m)} ${to.y}`
  return `${from.d} – ${to.d} ${short(to.m)} ${to.y}`
}

export function monthName(date: IsoDate): string {
  return MONTHS[parts(date).m - 1]
}

export function dayOfMonth(date: IsoDate): number {
  return parts(date).d
}

export function shortWeekday(date: IsoDate): string {
  return WEEKDAYS[weekday(date)].slice(0, 3)
}
