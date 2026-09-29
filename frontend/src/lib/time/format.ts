import type { HhMm, IsoDate } from './buildingTime'

/**
 * The one place dates and clock times are turned into words. Every label in the app —
 * a toast, a form, the calendar heading, a closure's dates — reads the same way because it
 * comes from here; and when the UI is localised, the names change here, not in forty
 * components.
 *
 * Dates arrive as the API's calendar strings ("2026-09-29", "10:30"), never as a browser
 * `Date` in local time: a building's day is a building's day whatever zone the viewer is
 * in. Names are fixed English tables rather than Intl's, whose short month for September
 * is "Sep" or "Sept" depending on the ICU version — the backend's own messages say "Sep".
 */

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

function parts(date: IsoDate) {
  const d = new Date(`${date}T00:00:00Z`)
  return { y: d.getUTCFullYear(), m: d.getUTCMonth(), d: d.getUTCDate(), w: d.getUTCDay() }
}

export type DateStyle =
  /** "Tue 29 Sep" — lists, toasts, anywhere space is tight and the year is obvious. */
  | 'short'
  /** "Tue 29 Sep 2026" — when the year matters (a series' end, a closure). */
  | 'medium'
  /** "Tuesday 29 September 2026" — headings and pickers. */
  | 'long'
  /** "29 Sep" — ranges ("29 Sep – 3 Oct"). */
  | 'day-month'

/** A calendar date in words, in one of a few fixed styles. */
export function formatDay(date: IsoDate, style: DateStyle = 'short'): string {
  const p = parts(date)
  switch (style) {
    case 'short':
      return `${WEEKDAYS[p.w].slice(0, 3)} ${p.d} ${MONTHS[p.m].slice(0, 3)}`
    case 'medium':
      return `${WEEKDAYS[p.w].slice(0, 3)} ${p.d} ${MONTHS[p.m].slice(0, 3)} ${p.y}`
    case 'long':
      return `${WEEKDAYS[p.w]} ${p.d} ${MONTHS[p.m]} ${p.y}`
    case 'day-month':
      return `${p.d} ${MONTHS[p.m].slice(0, 3)}`
  }
}

/** "September 2026" */
export function formatMonthYear(date: IsoDate): string {
  const p = parts(date)
  return `${MONTHS[p.m]} ${p.y}`
}

/** "September", "Sep" */
export function formatMonth(date: IsoDate, style: 'long' | 'short' = 'long'): string {
  const name = MONTHS[parts(date).m]
  return style === 'short' ? name.slice(0, 3) : name
}

/** "Tuesday", "Tue" */
export function formatWeekday(date: IsoDate, style: 'long' | 'short' = 'long'): string {
  const name = WEEKDAYS[parts(date).w]
  return style === 'short' ? name.slice(0, 3) : name
}

/**
 * "29 Sep – 3 Oct 2026", "29 – 30 Sep 2026", "30 Dec 2026 – 2 Jan 2027" — a span of days,
 * saying each part only once.
 */
export function formatDaySpan(from: IsoDate, to: IsoDate): string {
  const a = parts(from)
  const b = parts(to)
  const short = (m: number) => MONTHS[m].slice(0, 3)
  if (from === to) return formatDay(from, 'medium')
  if (a.y !== b.y) return `${a.d} ${short(a.m)} ${a.y} – ${b.d} ${short(b.m)} ${b.y}`
  if (a.m !== b.m) return `${a.d} ${short(a.m)} – ${b.d} ${short(b.m)} ${b.y}`
  return `${a.d} – ${b.d} ${short(b.m)} ${b.y}`
}

/**
 * A clock time. One convention for the whole app: the 24-hour clock the operating hours,
 * the booking form and the API already use. "24:00" (the end of a day) stays "24:00".
 */
export function formatClock(time: HhMm | string): string {
  return time
}

/** "10:00–11:30" */
export function formatClockRange(from: HhMm | string, to: HhMm | string): string {
  return `${formatClock(from)}–${formatClock(to)}`
}

/** A browser-local Date's own calendar day as an API date string — for date pickers, whose value is a Date. */
export function localDateToIso(d: Date): IsoDate {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
