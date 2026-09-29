import { describe, expect, it } from 'vitest'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import {
  choiceFor,
  defaultEndDate,
  describeRecurrence,
  endAfterMonths,
  endAfterWeeks,
  Frequency,
  maxMonths,
  maxWeeks,
  MonthlyRepeat,
  monthsUntil,
  repeatPresets,
  weekdayPosition,
  weeksUntil,
} from './recurrence'

// Tue 29 Sep 2026; the building is open Sun–Thu.
const TUE = '2026-09-29'
const SUN_THU = [0, 1, 2, 3, 4]

describe('repeatPresets', () => {
  it('words the choices from the date, with workdays from the open days', () => {
    expect(repeatPresets(TUE, SUN_THU).map((p) => p.label)).toEqual([
      'Does not repeat',
      'Every workday (Sun–Thu)',
      'Daily',
      'Weekly on Tuesday',
      'Monthly on day 29',
      'Custom…',
    ])
  })

  it('leaves out "every workday" for a room open every day', () => {
    expect(repeatPresets(TUE, [0, 1, 2, 3, 4, 5, 6]).map((p) => p.choice)).not.toContain('workdays')
  })

  it('builds the rule for a choice', () => {
    const weekly = repeatPresets(TUE, SUN_THU).find((p) => p.choice === 'weekly')!
    expect(weekly.rule!('2026-10-27')).toEqual({ frequency: Frequency.Weekly, interval: 1, weekdays: [2], monthlyRepeat: 0, endDate: '2026-10-27' })
  })
})

describe('describeRecurrence', () => {
  const rule = (patch: Partial<RecurrenceDto>): RecurrenceDto => ({ frequency: Frequency.Weekly, interval: 1, weekdays: [2], monthlyRepeat: 0, endDate: '2026-10-27', ...patch })

  it('reads like Teams', () => {
    expect(describeRecurrence(rule({}), TUE)).toBe('Occurs every Tuesday until Tue 27 Oct')
    expect(describeRecurrence(rule({ weekdays: [4, 2] }), TUE)).toBe('Occurs every Tuesday and Thursday until Tue 27 Oct')
    expect(describeRecurrence(rule({ interval: 2, weekdays: [0, 2, 4] }), TUE)).toBe(
      'Occurs every 2 weeks on Sunday, Tuesday and Thursday until Tue 27 Oct',
    )
    // A run of days reads as a range even without the room's open days (the calendar's detail view).
    expect(describeRecurrence(rule({ weekdays: SUN_THU }), TUE)).toBe('Occurs every Sun–Thu until Tue 27 Oct')
    expect(describeRecurrence(rule({ weekdays: SUN_THU }), TUE, SUN_THU)).toBe('Occurs every workday (Sun–Thu) until Tue 27 Oct')
    expect(describeRecurrence(rule({ frequency: Frequency.Daily, interval: 3 }), TUE)).toBe('Occurs every 3 days until Tue 27 Oct')
  })

  it('says monthly by date or by weekday position', () => {
    const monthly = rule({ frequency: Frequency.Monthly, endDate: '2026-12-31' })
    expect(describeRecurrence(monthly, TUE)).toBe('Occurs on day 29 of every month until Thu 31 Dec')
    expect(describeRecurrence({ ...monthly, monthlyRepeat: MonthlyRepeat.OnWeekday }, '2026-10-13')).toBe(
      'Occurs on the 2nd Tuesday of every month until Thu 31 Dec',
    )
    expect(describeRecurrence({ ...monthly, monthlyRepeat: MonthlyRepeat.OnWeekday, interval: 2 }, '2026-10-29')).toBe(
      'Occurs on the last Thursday of every 2 months until Thu 31 Dec',
    )
  })
})

describe('weekdayPosition', () => {
  it('names the weekday position, with the 5th week as "last"', () => {
    expect(weekdayPosition('2026-10-06')).toBe('the 1st Tuesday')
    expect(weekdayPosition('2026-10-29')).toBe('the last Thursday')
  })
})

describe('defaultEndDate', () => {
  it('is four weeks out, three months for monthly, capped at the last allowed date', () => {
    expect(defaultEndDate(TUE, Frequency.Weekly, '2026-12-27')).toBe('2026-10-26')
    expect(defaultEndDate(TUE, Frequency.Monthly, '2026-12-27')).toBe('2026-12-27')
    expect(defaultEndDate(TUE, Frequency.Monthly, '2027-06-01')).toBe('2026-12-29')
    expect(defaultEndDate(TUE, Frequency.Daily, '2026-10-10')).toBe('2026-10-10')
  })
})

describe('choiceFor', () => {
  it('finds the preset a rule matches, whatever its end date, else "custom"', () => {
    const base = { interval: 1, monthlyRepeat: 0, endDate: '2026-11-15' } as const
    expect(choiceFor(null, TUE, SUN_THU)).toBe('none')
    expect(choiceFor({ ...base, frequency: Frequency.Weekly, weekdays: [2] }, TUE, SUN_THU)).toBe('weekly')
    expect(choiceFor({ ...base, frequency: Frequency.Weekly, weekdays: [4, 3, 2, 1, 0] }, TUE, SUN_THU)).toBe('workdays')
    expect(choiceFor({ ...base, frequency: Frequency.Weekly, weekdays: [2, 4] }, TUE, SUN_THU)).toBe('custom')
  })
})

describe('weekly occurrence count', () => {
  it('counts the first week and converts back to the last occurrence date', () => {
    expect(weeksUntil('2026-09-29', '2026-09-29')).toBe(1)
    expect(weeksUntil('2026-09-29', '2026-10-20')).toBe(4)
    expect(endAfterWeeks('2026-09-29', 4, '2026-12-31')).toBe('2026-10-20')
  })

  it('never runs past the series horizon', () => {
    expect(maxWeeks('2026-09-29', '2026-10-20')).toBe(4)
    expect(endAfterWeeks('2026-09-29', 10, '2026-10-20')).toBe('2026-10-20')
    expect(endAfterWeeks('2026-09-29', 0, '2026-12-31')).toBe('2026-09-29')
  })
})

describe('monthly occurrence count', () => {
  it('counts the first month and lands on the last occurrence', () => {
    expect(monthsUntil('2026-09-29', '2026-09-29')).toBe(1)
    expect(monthsUntil('2026-09-29', '2026-11-29')).toBe(3)
    expect(endAfterMonths('2026-09-29', 3, '2027-12-31')).toBe('2026-11-29')
  })

  it('clamps a late day like the occurrences do, and stops at the horizon', () => {
    expect(endAfterMonths('2026-10-31', 2, '2027-12-31')).toBe('2026-11-30')
    expect(maxMonths('2026-09-29', '2026-12-15')).toBe(3)
    expect(endAfterMonths('2026-09-29', 12, '2026-12-15')).toBe('2026-11-29')
  })
})
