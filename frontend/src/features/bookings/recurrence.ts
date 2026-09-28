import { addDays, formatDate } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import { formatDays } from '@/features/bookings/format'

// Teams-style repeat options for the booking form: the quick choices in the Repeat
// dropdown, the "Occurs every…" sentence under it, and sensible default end dates. All on
// "YYYY-MM-DD" building-local dates, so no timezone math.

export const Frequency = { Daily: 0, Weekly: 1, Monthly: 2 } as const
export const MonthlyRepeat = { OnDay: 0, OnWeekday: 1 } as const

export type RepeatChoice = 'none' | 'workdays' | 'daily' | 'weekly' | 'monthly' | 'custom'

/** What the Repeat field holds: a quick choice (with its end date) or a custom rule. */
export interface RepeatValue {
  choice: RepeatChoice
  /** The end date for a quick choice; a custom rule carries its own. */
  endDate: IsoDate | null
  custom: RecurrenceDto | null
}

export const NO_REPEAT: RepeatValue = { choice: 'none', endDate: null, custom: null }

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const ORDINALS = ['', '1st', '2nd', '3rd', '4th', 'last']

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
  const presets: RepeatPreset[] = [{ choice: 'none', label: 'Does not repeat', rule: null }]

  if (workdays.length >= 2 && workdays.length < 7) {
    presets.push({
      choice: 'workdays',
      label: `Every workday (${formatDays(workdays)})`,
      rule: (endDate) => ({ frequency: Frequency.Weekly, interval: 1, weekdays: workdays, monthlyRepeat: 0, endDate }),
    })
  }

  presets.push(
    {
      choice: 'daily',
      label: 'Daily',
      rule: (endDate) => ({ frequency: Frequency.Daily, interval: 1, weekdays: [], monthlyRepeat: 0, endDate }),
    },
    {
      choice: 'weekly',
      label: `Weekly on ${DAY_NAMES[weekday]}`,
      rule: (endDate) => ({ frequency: Frequency.Weekly, interval: 1, weekdays: [weekday], monthlyRepeat: 0, endDate }),
    },
    {
      choice: 'monthly',
      label: `Monthly on day ${parts(date).d}`,
      rule: (endDate) => ({ frequency: Frequency.Monthly, interval: 1, weekdays: [], monthlyRepeat: MonthlyRepeat.OnDay, endDate }),
    },
    { choice: 'custom', label: 'Custom…', rule: null },
  )
  return presets
}

function joinNames(names: string[]): string {
  if (names.length <= 1) return names.join('')
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`
}

/** "the 2nd Tuesday" / "the last Thursday" for a date. */
export function weekdayPosition(date: IsoDate): string {
  return `the ${ORDINALS[weekOfMonth(date)]} ${DAY_NAMES[weekdayOf(date)]}`
}

/**
 * Teams' summary line: "Occurs every Tuesday and Thursday until Tue 27 Oct",
 * "Occurs every 2 weeks on Tuesday until …", "Occurs on the 2nd Tuesday of every month until …".
 */
export function describeRecurrence(rule: RecurrenceDto, date: IsoDate, openDays: number[] = []): string {
  const until = ` until ${formatDate(rule.endDate)}`
  const n = rule.interval

  if (rule.frequency === Frequency.Daily) {
    return `Occurs ${n === 1 ? 'every day' : `every ${n} days`}${until}`
  }

  if (rule.frequency === Frequency.Weekly) {
    const days = [...rule.weekdays].sort((a, b) => a - b)
    const workdays = [...openDays].sort((a, b) => a - b)
    if (n === 1 && workdays.length >= 2 && workdays.length < 7 && days.join() === workdays.join()) {
      return `Occurs every workday (${formatDays(workdays)})${until}`
    }
    const names = joinNames(days.map((d) => DAY_NAMES[d]))
    return `Occurs ${n === 1 ? `every ${names}` : `every ${n} weeks on ${names}`}${until}`
  }

  const every = n === 1 ? 'every month' : `every ${n} months`
  const on = rule.monthlyRepeat === MonthlyRepeat.OnWeekday ? weekdayPosition(date) : `day ${parts(date).d}`
  return `Occurs on ${on} of ${every}${until}`
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
