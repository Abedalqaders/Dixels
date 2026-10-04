import i18n from '@/i18n'
import { MonthlyRepeat, RecurrenceFrequency } from './api/bookingsApi'
import { addDays, formatDate } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import { formatDays, joinNames, weekdayName } from '@/features/bookings/format'

// Teams-style repeat options for the booking form: the quick choices in the Repeat
// dropdown, the "Occurs every…" sentence under it, and sensible default end dates. All on
// "YYYY-MM-DD" building-local dates, so no timezone math. The wording is in the reader's
// language: each sentence is one text with the pieces ({every}, {until}…) slotted in, so a
// language can put them in its own order, and counts use its own plural forms.

// The API module owns these (they mirror the C# enums); this is the name the form code uses.
export const Frequency = RecurrenceFrequency
export { MonthlyRepeat }

export type RepeatChoice = 'none' | 'workdays' | 'daily' | 'weekly' | 'monthly' | 'custom'

/** What the Repeat field holds: a quick choice (with its end date) or a custom rule. */
export interface RepeatValue {
  choice: RepeatChoice
  /** The end date for a quick choice; a custom rule carries its own. */
  endDate: IsoDate | null
  custom: RecurrenceDto | null
}

export const NO_REPEAT: RepeatValue = { choice: 'none', endDate: null, custom: null }

/** "the 2nd Tuesday" by week of the month (1–4; 5, a date in the 5th week, is "the last"). */
const POSITIONS = {
  1: 'Repeat:First',
  2: 'Repeat:Second',
  3: 'Repeat:Third',
  4: 'Repeat:Fourth',
  5: 'Repeat:Last',
} as const

function parts(date: IsoDate) {
  const [y, m, d] = date.split('-').map(Number)
  return { y, m, d }
}

export function weekdayOf(date: IsoDate): number {
  return new Date(`${date}T00:00:00Z`).getUTCDay()
}

/** 1–4 for "the 2nd Tuesday"; 5 (a date in the month's 5th week) repeats as "the last". */
export function weekOfMonth(date: IsoDate): number {
  return Math.floor((parts(date).d - 1) / 7) + 1
}

/** The same day `n` months on, clamped to the month's length. */
function addMonths(date: IsoDate, n: number): IsoDate {
  const { y, m, d } = parts(date)
  const total = y * 12 + (m - 1) + n
  const year = Math.floor(total / 12)
  const month = (total % 12) + 1
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate()
  return `${year}-${String(month).padStart(2, '0')}-${String(Math.min(d, last)).padStart(2, '0')}`
}

/** Four weeks out (three months for monthly), never past the last date a series may reach. */
export function defaultEndDate(date: IsoDate, frequency: number, lastDate: IsoDate): IsoDate {
  const end = frequency === Frequency.Monthly ? addMonths(date, 3) : addDays(date, 27)
  return end < lastDate ? end : lastDate
}

function dayNumber(date: IsoDate): number {
  return Date.parse(`${date}T00:00:00Z`) / 86_400_000
}

/** The most weeks a weekly series starting on `date` can run before the series horizon. */
export function maxWeeks(date: IsoDate, lastDate: IsoDate): number {
  return Math.max(1, Math.floor((dayNumber(lastDate) - dayNumber(date)) / 7) + 1)
}

/** How many weekly occurrences a series from `date` to `endDate` has — the first one counts. */
export function weeksUntil(date: IsoDate, endDate: IsoDate): number {
  return Math.max(1, Math.floor((dayNumber(endDate) - dayNumber(date)) / 7) + 1)
}

/** The end date for `weeks` weekly occurrences from `date`: the day of the last one. */
export function endAfterWeeks(date: IsoDate, weeks: number, lastDate: IsoDate): IsoDate {
  const end = addDays(date, (Math.min(Math.max(1, weeks), maxWeeks(date, lastDate)) - 1) * 7)
  return end < lastDate ? end : lastDate
}

/** The most monthly occurrences a series starting on `date` can have before the horizon. */
export function maxMonths(date: IsoDate, lastDate: IsoDate): number {
  let n = 1
  while (addMonths(date, n) <= lastDate) n++
  return n
}

/** How many monthly occurrences a series from `date` to `endDate` has — the first one counts. */
export function monthsUntil(date: IsoDate, endDate: IsoDate): number {
  let n = 1
  while (addMonths(date, n) <= endDate) n++
  return n
}

/** The end date for `months` monthly occurrences from `date`: the day of the last one
 * (clamped like the occurrences are — day 31 lands on the 30th in a 30-day month). */
export function endAfterMonths(date: IsoDate, months: number, lastDate: IsoDate): IsoDate {
  const end = addMonths(date, Math.min(Math.max(1, months), maxMonths(date, lastDate)) - 1)
  return end < lastDate ? end : lastDate
}

export interface RepeatPreset {
  choice: RepeatChoice
  label: string
  /** The rule this choice stands for, given an end date; null for "Does not repeat" and "Custom…". */
  rule: ((endDate: IsoDate) => RecurrenceDto) | null
}

/**
 * The Repeat dropdown for a first date: Teams' quick choices, worded from that date
 * ("Weekly on Tuesday", "Monthly on day 29"). "Every workday" uses the room's own open
 * days (Sun–Thu here), so it never lands on a day the room is shut.
 */
export function repeatPresets(date: IsoDate, openDays: number[]): RepeatPreset[] {
  const weekday = weekdayOf(date)
  const workdays = [...openDays].sort((a, b) => a - b)
  const presets: RepeatPreset[] = [{ choice: 'none', label: i18n.t('Repeat:None'), rule: null }]

  if (workdays.length >= 2 && workdays.length < 7) {
    presets.push({
      choice: 'workdays',
      label: i18n.t('Repeat:Workdays', { days: formatDays(workdays) }),
      rule: (endDate) => ({ frequency: Frequency.Weekly, interval: 1, weekdays: workdays, monthlyRepeat: 0, endDate }),
    })
  }

  presets.push(
    {
      choice: 'daily',
      label: i18n.t('Repeat:Daily'),
      rule: (endDate) => ({ frequency: Frequency.Daily, interval: 1, weekdays: [], monthlyRepeat: 0, endDate }),
    },
    {
      choice: 'weekly',
      label: i18n.t('Repeat:WeeklyOn', { weekday: weekdayName(weekday) }),
      rule: (endDate) => ({ frequency: Frequency.Weekly, interval: 1, weekdays: [weekday], monthlyRepeat: 0, endDate }),
    },
    {
      choice: 'monthly',
      label: i18n.t('Repeat:MonthlyOnDay', { day: parts(date).d }),
      rule: (endDate) => ({ frequency: Frequency.Monthly, interval: 1, weekdays: [], monthlyRepeat: MonthlyRepeat.OnDay, endDate }),
    },
    { choice: 'custom', label: i18n.t('Repeat:CustomChoice'), rule: null },
  )
  return presets
}

/** "the 2nd Tuesday" / "the last Thursday" for a date. */
export function weekdayPosition(date: IsoDate): string {
  return i18n.t(POSITIONS[weekOfMonth(date) as keyof typeof POSITIONS], { weekday: weekdayName(weekdayOf(date)) })
}

/**
 * Teams' summary line: "Occurs every Tuesday and Thursday until Tue 27 Oct",
 * "Occurs every 2 weeks on Tuesday until …", "Occurs on the 2nd Tuesday of every month until …".
 */
export function describeRecurrence(rule: RecurrenceDto, date: IsoDate, openDays: number[] = []): string {
  const until = formatDate(rule.endDate)
  const n = rule.interval
  const occurs = (every: string) => i18n.t('Repeat:Occurs', { every, until })

  if (rule.frequency === Frequency.Daily) {
    return occurs(i18n.t('Repeat:EveryNDays', { count: n }))
  }

  if (rule.frequency === Frequency.Weekly) {
    const days = [...rule.weekdays].sort((a, b) => a - b)
    const workdays = [...openDays].sort((a, b) => a - b)
    if (n === 1 && workdays.length >= 2 && workdays.length < 7 && days.join() === workdays.join()) {
      return occurs(i18n.t('Repeat:EveryWorkday', { days: formatDays(workdays) }))
    }
    if (days.length === 7) {
      return occurs(n === 1 ? i18n.t('Repeat:EveryNDays', { count: 1 }) : i18n.t('Repeat:EveryNWeeksEveryDay', { count: n }))
    }
    // Three or more days in a row read as a range, like Teams' "every weekday": "every Sun–Thu".
    const isRun = days.length >= 3 && days[days.length - 1] - days[0] === days.length - 1
    const names = isRun ? formatDays(days) : joinNames(days.map((d) => weekdayName(d)))
    return occurs(n === 1 ? i18n.t('Repeat:EveryWeekdays', { days: names }) : i18n.t('Repeat:EveryNWeeksOn', { count: n, days: names }))
  }

  const every = i18n.t('Repeat:EveryNMonths', { count: n })
  const on = rule.monthlyRepeat === MonthlyRepeat.OnWeekday ? weekdayPosition(date) : i18n.t('Repeat:DayOfMonth', { day: parts(date).d })
  return i18n.t('Repeat:OccursMonthly', { on, every, until })
}

/** Which dropdown choice a rule is — a preset if it matches one exactly (ignoring the end date), else "Custom…". */
export function choiceFor(rule: RecurrenceDto | null, date: IsoDate, openDays: number[]): RepeatChoice {
  if (!rule) return 'none'
  const same = (a: RecurrenceDto, b: RecurrenceDto) =>
    a.frequency === b.frequency &&
    a.interval === b.interval &&
    a.monthlyRepeat === b.monthlyRepeat &&
    [...a.weekdays].sort().join() === [...b.weekdays].sort().join()
  const match = repeatPresets(date, openDays).find((p) => p.rule && same(p.rule(rule.endDate), rule))
  return match?.choice ?? 'custom'
}
