import { describe, expect, it } from 'vitest'
import type { BookableBuildingDto } from './api/bookingsApi'
import { biggestRoom, buildingOpenRanges, buildingRules, canBookOn, longestRoomLength } from './buildingRules'
import { startTimes } from './freeTimes'

const h = (hours: number) => hours * 60
const room = (capacity: number, maxDurationMinutes: number) => ({ capacity, maxDurationMinutes: { value: maxDurationMinutes, source: 'Space' } })

// Sun–Thu 08:00–18:00, 30 days ahead; rooms seat 4 and 12, and allow 1h and 3h.
const building = {
  slotMinutes: 15,
  minLeadMinutes: 15,
  maxHorizonDays: 30,
  days: [0, 1, 2, 3, 4],
  hours: { isOpen24Hours: false, open: '08:00', close: '18:00' },
  floors: [{ id: 'f', name: 'L1', floorNumber: 1, spaces: [room(4, 60), room(12, 180)] }],
} as unknown as BookableBuildingDto

// Monday 5 Oct 2026, 10:00.
const now = { date: '2026-10-05', minutes: h(10) }

describe('buildingOpenRanges', () => {
  it('is the opening hours on an open day, nothing on a closed one', () => {
    expect(buildingOpenRanges(building, '2026-10-05')).toEqual([{ start: h(8), end: h(18) }])
    expect(buildingOpenRanges(building, '2026-10-09')).toEqual([]) // Friday
  })

  it('carries hours that run past midnight into the next morning', () => {
    const nights = { ...building, hours: { isOpen24Hours: false, open: '22:00', close: '06:00' } }
    // Monday: the early hours from Sunday night, and Monday night from 22:00.
    expect(buildingOpenRanges(nights, '2026-10-05')).toEqual([
      { start: 0, end: h(6) },
      { start: h(22), end: h(24) },
    ])
    // Sunday: Saturday night was closed, so only Sunday night.
    expect(buildingOpenRanges(nights, '2026-10-04')).toEqual([{ start: h(22), end: h(24) }])
    // Friday: closed, but Thursday night runs into its morning.
    expect(buildingOpenRanges(nights, '2026-10-09')).toEqual([{ start: 0, end: h(6) }])
  })

  it('is the whole day on an open day of a 24h building', () => {
    const always = { ...building, hours: { isOpen24Hours: true, open: '00:00', close: '00:00' } }
    expect(buildingOpenRanges(always, '2026-10-05')).toEqual([{ start: 0, end: h(24) }])
  })
})

describe('buildingRules', () => {
  it('starts today after now + notice, and allows the longest any room does', () => {
    const rules = buildingRules(building, '2026-10-05', now)
    expect(startTimes(rules)[0]).toBe(h(10) + 15)
    expect(rules.maxDuration).toBe(180)
  })

  it('starts another day at opening', () => {
    expect(startTimes(buildingRules(building, '2026-10-06', now))[0]).toBe(h(8))
  })
})

describe('canBookOn', () => {
  it('is false on closed days, past days and past the booking window', () => {
    expect(canBookOn(building, '2026-10-05', now)).toBe(true)
    expect(canBookOn(building, '2026-10-09', now)).toBe(false) // Friday
    expect(canBookOn(building, '2026-10-04', now)).toBe(false) // yesterday
    expect(canBookOn(building, '2026-11-05', now)).toBe(false) // 31 days ahead
  })

  it('is false today once the building has closed', () => {
    expect(canBookOn(building, '2026-10-05', { date: '2026-10-05', minutes: h(17) + 50 })).toBe(false)
  })
})

describe('room limits', () => {
  it('finds the biggest room and the longest booking', () => {
    expect(biggestRoom(building)).toBe(12)
    expect(longestRoomLength(building)).toBe(180)
  })
})
