import { describe, expect, it } from 'vitest'
import { addDays, formatDate, nextSlot, nowInZone, slotTimes, toLocalDateTime } from './buildingTime'

describe('nowInZone', () => {
  it("reads today and the time on the building's clock, not the browser's", () => {
    // 22:30 UTC on 28 Sep is already 01:30 on 29 Sep in Amman (UTC+3).
    const at = new Date('2026-09-28T22:30:00Z')

    expect(nowInZone('Asia/Amman', at)).toEqual({ date: '2026-09-29', minutes: 90 })
    expect(nowInZone('UTC', at)).toEqual({ date: '2026-09-28', minutes: 22 * 60 + 30 })
  })
})

describe('addDays', () => {
  it('crosses month and year boundaries', () => {
    expect(addDays('2026-09-30', 1)).toBe('2026-10-01')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28')
  })

  it('never skips or repeats a day across a DST change', () => {
    expect(addDays('2026-03-28', 1)).toBe('2026-03-29')
    expect(addDays('2026-03-29', 1)).toBe('2026-03-30')
  })
})

describe('toLocalDateTime', () => {
  it('builds the offset-free value the API expects', () => {
    expect(toLocalDateTime('2026-09-29', '10:15')).toBe('2026-09-29T10:15:00')
  })

  it('turns 24:00 into midnight at the start of the next day', () => {
    expect(toLocalDateTime('2026-09-30', '24:00')).toBe('2026-10-01T00:00:00')
  })
})

describe('slot grid', () => {
  it('lists times on the grid, inclusive of both ends', () => {
    expect(slotTimes(15, 9 * 60, 10 * 60)).toEqual(['09:00', '09:15', '09:30', '09:45', '10:00'])
  })

  it('rounds up to the next boundary', () => {
    expect(nextSlot(9 * 60 + 7, 15)).toBe(9 * 60 + 15)
    expect(nextSlot(9 * 60 + 15, 15)).toBe(9 * 60 + 15)
  })
})

describe('formatDate', () => {
  it('formats a calendar date without shifting it through the browser zone', () => {
    expect(formatDate('2026-09-29')).toBe('Tue 29 Sep')
  })
})
