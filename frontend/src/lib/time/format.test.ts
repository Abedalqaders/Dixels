import { describe, expect, it } from 'vitest'
import { formatClock, formatClockRange, formatDay, formatDaySpan, formatMonthYear, formatWeekday, localDateToIso } from './format'

describe('formatDay', () => {
  it('has one spelling per style, independent of the browser zone', () => {
    expect(formatDay('2026-09-29')).toBe('Tue 29 Sep')
    expect(formatDay('2026-09-29', 'medium')).toBe('Tue 29 Sep 2026')
    expect(formatDay('2026-09-29', 'long')).toBe('Tuesday 29 September 2026')
    expect(formatDay('2026-09-29', 'day-month')).toBe('29 Sep')
  })
})

describe('formatDaySpan', () => {
  it('says each part once', () => {
    expect(formatDaySpan('2026-09-28', '2026-10-04')).toBe('28 Sep – 4 Oct 2026')
    expect(formatDaySpan('2026-09-28', '2026-09-30')).toBe('28 – 30 Sep 2026')
    expect(formatDaySpan('2026-12-28', '2027-01-03')).toBe('28 Dec 2026 – 3 Jan 2027')
    expect(formatDaySpan('2026-09-28', '2026-09-28')).toBe('Mon 28 Sep 2026')
  })
})

describe('formatClock', () => {
  it('is the 24-hour clock everywhere, and the end of a day stays 24:00', () => {
    expect(formatClock('09:00')).toBe('09:00')
    expect(formatClock('14:20')).toBe('14:20')
    expect(formatClock('24:00')).toBe('24:00')
    expect(formatClockRange('09:00', '10:30')).toBe('09:00–10:30')
  })
})

describe('names', () => {
  it('month and weekday', () => {
    expect(formatMonthYear('2026-09-01')).toBe('September 2026')
    expect(formatWeekday('2026-09-29', 'short')).toBe('Tue')
  })
})

describe('localDateToIso', () => {
  it("reads a picker's Date as the calendar day it shows", () => {
    expect(localDateToIso(new Date(2026, 8, 29, 23, 30))).toBe('2026-09-29')
  })
})
