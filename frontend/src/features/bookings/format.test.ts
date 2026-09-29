import { describe, expect, it } from 'vitest'
import { formatDays, formatDuration, formatHours } from './format'

describe('booking rule formatting', () => {
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
})
