// Wall-clock helpers for a building's timezone. Bookings are always entered and shown in
// the *building's* local time — never the browser's — so someone in London booking a
// room in Amman sees Amman's 09:00. The server does the real local→UTC conversion; the
// frontend only needs "what is today/now over there" and plain date arithmetic on
// "YYYY-MM-DD" strings, which the built-in Intl API covers without a date library.

/** A calendar date as "YYYY-MM-DD" (no time, no zone). */
export type IsoDate = string

/** A wall-clock time as "HH:mm" on a 24-hour clock. "24:00" means midnight at the end of the day. */
export type HhMm = string

export interface ZonedNow {
  date: IsoDate
  /** Minutes since local midnight, 0–1439. */
  minutes: number
}

/** Today's date and the current time in `timeZone` (an IANA name like "Asia/Amman"). */
export function nowInZone(timeZone: string, at: Date = new Date()): ZonedNow {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(at)

  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? '00'

  return {
    date: `${get('year')}-${get('month')}-${get('day')}`,
    minutes: Number(get('hour')) * 60 + Number(get('minute')),
  }
}

/** Calendar arithmetic on a date string — done in UTC so no DST shift can skip or repeat a day. */
export function addDays(date: IsoDate, days: number): IsoDate {
  const d = new Date(`${date}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

export function toMinutes(time: HhMm): number {
  const [h, m] = time.split(':').map(Number)
  return h * 60 + m
}

export function fromMinutes(minutes: number): HhMm {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`
}

/**
 * The offset-free local datetime the API expects ("2026-09-29T10:00:00"). "24:00" becomes
 * 00:00 on the next day, so a booking can end exactly at midnight.
 */
export function toLocalDateTime(date: IsoDate, time: HhMm): string {
  const minutes = toMinutes(time)
  if (minutes >= 24 * 60) {
    return `${addDays(date, 1)}T${fromMinutes(minutes - 24 * 60)}:00`
  }
  return `${date}T${time}:00`
}

/** Every time on the slot grid from `from` to `to` inclusive, e.g. 00:00, 00:15, … */
export function slotTimes(slotMinutes: number, from = 0, to = 24 * 60): HhMm[] {
  const times: HhMm[] = []
  for (let m = from; m <= to; m += slotMinutes) {
    times.push(fromMinutes(m))
  }
  return times
}

/** The first slot boundary at or after `minutes` (e.g. 09:07 → 09:15 on a 15-minute grid). */
export function nextSlot(minutes: number, slotMinutes: number): number {
  return Math.ceil(minutes / slotMinutes) * slotMinutes
}

const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/**
 * "Tue 29 Sep" — a calendar date for display, independent of the browser's zone. Built
 * from fixed names rather than Intl's "short" month, which varies by ICU version
 * ("Sep" vs "Sept") and would drift from the backend's messages.
 */
export function formatDate(date: IsoDate): string {
  const d = new Date(`${date}T00:00:00Z`)
  return `${DAY_NAMES[d.getUTCDay()]} ${d.getUTCDate()} ${MONTH_NAMES[d.getUTCMonth()]}`
}

/** "HH:mm" out of the API's offset-free local datetime ("2026-09-29T10:00:00"). */
export function timeOf(localDateTime: string): HhMm {
  return localDateTime.slice(11, 16)
}

/** "YYYY-MM-DD" out of the API's offset-free local datetime. */
export function dateOf(localDateTime: string): IsoDate {
  return localDateTime.slice(0, 10)
}
