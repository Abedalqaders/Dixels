import { describe, expect, it } from 'vitest'
import { addMonths, clock12, gridMonthFor, isValidIsoDate, monthGrid, rangeLabel, shiftDate, startOfWeek, visibleRange } from './calendarDates'

describe('calendarDates', () => {
  it('starts weeks on Sunday', () => {
    expect(startOfWeek('2026-09-30')).toBe('2026-09-27') // Wed → Sun
    expect(startOfWeek('2026-09-27')).toBe('2026-09-27')
  })

  it('gives each view the days it shows', () => {
    expect(visibleRange('day', '2026-09-30')).toEqual({ from: '2026-09-30', to: '2026-10-01' })
    expect(visibleRange('week', '2026-09-30')).toEqual({ from: '2026-09-27', to: '2026-10-04' })
    // September 2026 starts on a Tuesday and needs 5 rows: 30 Aug – 3 Oct.
    expect(visibleRange('month', '2026-09-15')).toEqual({ from: '2026-08-30', to: '2026-10-04' })
  })

  it('sizes the month grid to the month', () => {
    expect(monthGrid('2026-02-10')).toEqual({ start: '2026-02-01', weeks: 4 }) // Feb 2026 starts on Sunday
    expect(monthGrid('2026-08-10').weeks).toBe(6) // starts Saturday, 31 days
  })

  it('steps by a day, a week or a month, clamping the day of month', () => {
    expect(shiftDate('day', '2026-09-30', 1)).toBe('2026-10-01')
    expect(shiftDate('week', '2026-09-30', -1)).toBe('2026-09-23')
    expect(shiftDate('month', '2026-01-31', 1)).toBe('2026-02-28')
    expect(addMonths('2026-12-15', 1)).toBe('2027-01-15')
    expect(addMonths('2026-01-15', -1)).toBe('2025-12-15')
  })

  it('accepts only real dates', () => {
    expect(isValidIsoDate('2026-09-30')).toBe(true)
    expect(isValidIsoDate('2028-02-29')).toBe(true)
    expect(isValidIsoDate('2026-02-30')).toBe(false)
    expect(isValidIsoDate('2026-13-45')).toBe(false)
    expect(isValidIsoDate('yesterday')).toBe(false)
    expect(isValidIsoDate(null)).toBe(false)
  })

  it("puts every view inside one month's grid", () => {
    // The week of Sun 27 Sep – Sat 3 Oct starts in September, and September's grid runs to 3 Oct.
    expect(gridMonthFor('week', '2026-10-02')).toBe('2026-09-01')
    const sep = visibleRange('month', '2026-09-01')
    const week = visibleRange('week', '2026-10-02')
    expect(week.from >= sep.from && week.to <= sep.to).toBe(true)
    expect(gridMonthFor('day', '2026-10-02')).toBe('2026-10-01')
  })

  it('labels the range for the toolbar', () => {
    expect(rangeLabel('day', '2026-09-29')).toBe('Tuesday 29 September 2026')
    expect(rangeLabel('week', '2026-09-23')).toBe('20 – 26 Sep 2026')
    expect(rangeLabel('week', '2026-09-30')).toBe('27 Sep – 3 Oct 2026')
    expect(rangeLabel('week', '2026-12-30')).toBe('27 Dec 2026 – 2 Jan 2027')
    expect(rangeLabel('month', '2026-09-29')).toBe('September 2026')
  })
})

describe('clock12', () => {
  it('reads like a wall clock: whole hours without minutes, noon and midnight as 12', () => {
    expect(clock12('09:00')).toBe('9 AM')
    expect(clock12('14:20')).toBe('2:20 PM')
    expect(clock12('12:00')).toBe('12 PM')
    expect(clock12('00:30')).toBe('12:30 AM')
    expect(clock12('24:00')).toBe('12 AM')
  })
})
