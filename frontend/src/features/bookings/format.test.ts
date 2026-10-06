import { describe, expect, it } from 'vitest'
import { setLanguage } from '@/i18n'
import { bookingTitle, formatDays, formatDuration, formatHours } from './format'

describe('booking rule formatting', () => {
  it("names an untitled booking in the reader's language, and keeps a real title", async () => {
    await setLanguage('en')
    expect(bookingTitle('')).toBe('Booking')
    expect(bookingTitle('Standup')).toBe('Standup')
    await setLanguage('ar')
    expect(bookingTitle('')).toBe('حجز')
    await setLanguage('en')
  })

  it('describes operating days the same way the backend messages do', () => {
    expect(formatDays([0, 1, 2, 3, 4, 5, 6])).toBe('every day')
    expect(formatDays([4, 0, 2, 1, 3])).toBe('Sun–Thu')
    expect(formatDays([0, 2, 4])).toBe('Sun, Tue, Thu')
  })

  it('describes hours and durations', () => {
    expect(formatHours({ isOpen24Hours: true, open: '00:00', close: '00:00' })).toBe('24 hours')
    expect(formatHours({ isOpen24Hours: false, open: '07:00', close: '20:00' })).toBe('07:00–20:00')
    expect(formatDuration(45)).toBe('45 min')
    expect(formatDuration(120)).toBe('2h')
    expect(formatDuration(150)).toBe('2h 30m')
  })

  it('says durations in Arabic with its plural forms', async () => {
    await setLanguage('ar')
    expect(formatDuration(1)).toBe('دقيقة واحدة')
    expect(formatDuration(5)).toBe('5 دقائق')
    expect(formatDuration(45)).toBe('45 دقيقة')
    expect(formatDuration(60)).toBe('ساعة')
    expect(formatDuration(120)).toBe('ساعتين')
    expect(formatDuration(180)).toBe('3 ساعات')
    expect(formatDuration(90)).toBe('ساعة و30 دقيقة')
  })

  it('says operating days and hours in Arabic, with full day names', async () => {
    await setLanguage('ar')
    expect(formatDays([0, 1, 2, 3, 4, 5, 6])).toBe('كل يوم')
    expect(formatDays([4, 0, 2, 1, 3])).toBe('الأحد إلى الخميس')
    expect(formatDays([0, 2, 4])).toBe('الأحد، الثلاثاء، الخميس')
    expect(formatHours({ isOpen24Hours: true, open: '00:00', close: '00:00' })).toBe('24 ساعة')
  })
})
