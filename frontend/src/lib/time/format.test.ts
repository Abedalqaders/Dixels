import { describe, expect, it } from 'vitest'
import i18n, { setLanguage } from '@/i18n'
import { formatClock, formatClockRange, formatDay, formatDaySpan, formatMonthYear, formatWeekday, isolateLtr, localDateToIso } from './format'

// The invisible isolate marks around left-to-right text in a right-to-left language.
const LRI = '⁦'
const PDI = '⁩'

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

describe('in Arabic', () => {
  it('uses Arabic day and month names, full length even in the short styles, with Western digits', async () => {
    await setLanguage('ar')
    expect(formatDay('2026-09-29')).toBe('الثلاثاء 29 سبتمبر')
    expect(formatDay('2026-09-29', 'long')).toBe('الثلاثاء 29 سبتمبر 2026')
    expect(formatMonthYear('2026-09-29')).toBe('سبتمبر 2026')
    expect(formatWeekday('2026-09-29', 'short')).toBe('الثلاثاء')
    expect(formatDaySpan('2026-09-28', '2026-10-04')).toBe('28 سبتمبر – 4 أكتوبر 2026')
  })

  it('keeps a time range in reading order: isolated left to right, so it never shows as 10:30–09:00', async () => {
    await setLanguage('ar')
    expect(formatClockRange('09:00', '10:30')).toBe(`${LRI}09:00–10:30${PDI}`)
    expect(isolateLtr('07:00 – 20:00')).toBe(`${LRI}07:00 – 20:00${PDI}`)
  })

  it('wraps a message’s own {start}–{end} once, around the range only', async () => {
    await setLanguage('ar')
    const label = i18n.t('Calendar:Item', { title: 'تخطيط', start: '09:00', end: '10:30', location: 'الغرفة 1' })
    expect(label).toBe(`تخطيط، ${LRI}09:00–10:30${PDI}، الغرفة 1`)
    expect(label.split(LRI)).toHaveLength(2)
    expect(label.split(PDI)).toHaveLength(2)
  })
})

describe('isolateLtr in English', () => {
  it('leaves text exactly as it was', () => {
    expect(isolateLtr('09:00–10:30')).toBe('09:00–10:30')
    expect(i18n.t('Calendar:Item', { title: 'Planning', start: '09:00', end: '10:30', location: 'Room 1' })).toBe('Planning, 09:00–10:30, Room 1')
  })
})
