import { currentLanguage, languageInfo } from '@/i18n'
import type { HhMm, IsoDate } from './buildingTime'

/**
 * The one place dates and clock times are turned into words. Every label in the app —
 * a toast, a form, the calendar heading, a closure's dates — reads the same way because it
 * comes from here; and when the UI is localised, the names change here, not in forty
 * components.
 *
 * Dates arrive as the API's calendar strings ("2026-09-29", "10:30"), never as a browser
 * `Date` in local time: a building's day is a building's day whatever zone the viewer is
 * in. English names are a fixed table rather than Intl's, whose short month for September
 * is "Sep" or "Sept" depending on the ICU version — the backend's own messages say "Sep".
 * Every other language (the list comes from the backend, so there can be any number) takes
 * its names from Intl, with Western digits. Arabic has no three-letter abbreviations: its
 * "short" names are the full ones, as Intl gives them.
 */

interface Names {
  weekdays: readonly string[]
  months: readonly string[]
  shortWeekdays: readonly string[]
  shortMonths: readonly string[]
  /** One or two letters, for a calendar's column headings: "S", "ح". */
  narrowWeekdays: readonly string[]
}

const EN_WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const EN_MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

const ENGLISH: Names = {
  weekdays: EN_WEEKDAYS,
  months: EN_MONTHS,
  shortWeekdays: EN_WEEKDAYS.map((w) => w.slice(0, 3)),
  shortMonths: EN_MONTHS.map((m) => m.slice(0, 3)),
  narrowWeekdays: EN_WEEKDAYS.map((w) => w[0]),
}

/** One language's names from Intl, worked out once. 4 Jan 2026 is a Sunday. */
function intlNames(locale: string): Names {
  const format = (options: Intl.DateTimeFormatOptions, date: Date) =>
    new Intl.DateTimeFormat(locale, { ...options, timeZone: 'UTC' }).format(date)
  const day = (i: number) => new Date(Date.UTC(2026, 0, 4 + i))
  const month = (i: number) => new Date(Date.UTC(2026, i, 1))
  const seven = Array.from({ length: 7 }, (_, i) => i)
  const twelve = Array.from({ length: 12 }, (_, i) => i)
  return {
    weekdays: seven.map((i) => format({ weekday: 'long' }, day(i))),
    months: twelve.map((i) => format({ month: 'long' }, month(i))),
    shortWeekdays: seven.map((i) => format({ weekday: 'short' }, day(i))),
    shortMonths: twelve.map((i) => format({ month: 'short' }, month(i))),
    narrowWeekdays: seven.map((i) => format({ weekday: 'narrow' }, day(i))),
  }
}

const byLanguage = new Map<string, Names>([['en', ENGLISH]])

function names(): Names {
  const language = currentLanguage()
  let found = byLanguage.get(language)
  if (!found) {
    found = intlNames(languageInfo(language).intl)
    byLanguage.set(language, found)
  }
  return found
}

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
  const n = names()
  switch (style) {
    case 'short':
      return `${n.shortWeekdays[p.w]} ${p.d} ${n.shortMonths[p.m]}`
    case 'medium':
      return `${n.shortWeekdays[p.w]} ${p.d} ${n.shortMonths[p.m]} ${p.y}`
    case 'long':
      return `${n.weekdays[p.w]} ${p.d} ${n.months[p.m]} ${p.y}`
    case 'day-month':
      return `${p.d} ${n.shortMonths[p.m]}`
  }
}

/** "September 2026" */
export function formatMonthYear(date: IsoDate): string {
  const p = parts(date)
  return `${names().months[p.m]} ${p.y}`
}

/** "September", "Sep" */
export function formatMonth(date: IsoDate, style: 'long' | 'short' = 'long'): string {
  const n = names()
  const m = parts(date).m
  return style === 'short' ? n.shortMonths[m] : n.months[m]
}

/** "Tuesday", "Tue", "T" */
export function formatWeekday(date: IsoDate, style: 'long' | 'short' | 'narrow' = 'long'): string {
  const n = names()
  const w = parts(date).w
  return style === 'narrow' ? n.narrowWeekdays[w] : style === 'short' ? n.shortWeekdays[w] : n.weekdays[w]
}

/**
 * "29 Sep – 3 Oct 2026", "29 – 30 Sep 2026", "30 Dec 2026 – 2 Jan 2027" — a span of days,
 * saying each part only once.
 */
export function formatDaySpan(from: IsoDate, to: IsoDate): string {
  const a = parts(from)
  const b = parts(to)
  const short = (m: number) => names().shortMonths[m]
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

/**
 * Keeps text that reads left to right (a time range) in that order inside a right-to-left
 * language. In Arabic "04:00–05:00" otherwise shows as "05:00–04:00": the bidi algorithm
 * treats the two numbers as right-to-left around the dash and swaps them. Wrapped in the
 * invisible isolate marks LRI…PDI (U+2066…U+2069), only when the language is RTL, so English
 * text stays exactly as it was. A plain string, so it works in JSX, toasts and aria-labels.
 * Messages that join {start}–{end} themselves wrap it in their own text instead (ar.json).
 */
export function isolateLtr(text: string): string {
  return languageInfo().dir === 'rtl' ? `⁦${text}⁩` : text
}

/** "10:00–11:30", kept in that order in a right-to-left language too. */
export function formatClockRange(from: HhMm | string, to: HhMm | string): string {
  return isolateLtr(`${formatClock(from)}–${formatClock(to)}`)
}

/** A browser-local Date's own calendar day as an API date string — for date pickers, whose value is a Date. */
export function localDateToIso(d: Date): IsoDate {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
