import { describe, expect, it } from 'vitest'
import type { BookableBuildingDto, BookableSpaceDto } from '@/features/bookings/api/bookingsApi'
import { suggestSlot, suggestWindow, suggestWindowForDay } from './suggestSlot'

// Open every day 08:00–18:00; its one room allows up to 3h.
const building = {
  timezone: 'Asia/Amman', // UTC+3
  slotMinutes: 15,
  minLeadMinutes: 15,
  maxHorizonDays: 30,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '08:00', close: '18:00' },
  floors: [{ id: 'f', name: 'L1', floorNumber: 1, spaces: [{ capacity: 8, maxDurationMinutes: { value: 180, source: 'Space' } }] }],
} as unknown as BookableBuildingDto

function spaceWith(open: string, close: string, maxDurationMinutes = 120): BookableSpaceDto {
  return {
    hours: { value: { isOpen24Hours: false, open, close }, source: 'Floor' },
    maxDurationMinutes: { value: maxDurationMinutes, source: 'Space' },
  } as BookableSpaceDto
}

describe('suggestSlot', () => {
  it('starts at the next slot after the minimum notice, on the building clock', () => {
    // 06:07 UTC is 09:07 in Amman; + 15 min notice = 09:22 → 09:30.
    const slot = suggestSlot(building, spaceWith('07:00', '20:00'), new Date('2026-09-29T06:07:00Z'))

    expect(slot).toEqual({ date: '2026-09-29', start: '09:30', end: '10:30' })
  })

  it('never suggests a time before the space opens', () => {
    // 03:00 in Amman.
    const slot = suggestSlot(building, spaceWith('08:00', '18:00'), new Date('2026-09-29T00:00:00Z'))

    expect(slot.start).toBe('08:00')
  })

  it('rolls to the next day once the space is closing', () => {
    // 19:30 in Amman, space closes at 20:00: no hour left today.
    const slot = suggestSlot(building, spaceWith('07:00', '20:00'), new Date('2026-09-29T16:30:00Z'))

    expect(slot).toEqual({ date: '2026-09-30', start: '07:00', end: '08:00' })
  })

  it('keeps the suggestion within the maximum duration', () => {
    const slot = suggestSlot(building, spaceWith('07:00', '20:00', 30), new Date('2026-09-29T06:07:00Z'))

    expect(slot.end).toBe('10:00')
  })
})

describe('suggestWindow', () => {
  it('starts the search at the next slot after the notice period, for an hour', () => {
    // 09:07 in Amman + 15 min notice → 09:30.
    expect(suggestWindow(building, new Date('2026-09-29T06:07:00Z'))).toEqual({
      date: '2026-09-29',
      start: '09:30',
      end: '10:30',
    })
  })

  it("moves to tomorrow's opening once today has no hour left", () => {
    // 17:30 in Amman; the building closes at 18:00.
    expect(suggestWindow(building, new Date('2026-09-29T14:30:00Z'))).toEqual({
      date: '2026-09-30',
      start: '08:00',
      end: '09:00',
    })
  })

  it('skips the days the building is closed', () => {
    // Thursday 1 Oct, 17:30 in Amman; closed Friday and Saturday.
    const sunToThu = { ...building, days: [0, 1, 2, 3, 4] }
    expect(suggestWindow(sunToThu, new Date('2026-10-01T14:30:00Z'))).toEqual({
      date: '2026-10-04',
      start: '08:00',
      end: '09:00',
    })
  })

  it('starts a picked future day at opening time', () => {
    expect(suggestWindowForDay(building, '2026-10-05', new Date('2026-09-29T06:07:00Z'), 60)).toEqual({
      date: '2026-10-05',
      start: '08:00',
      end: '09:00',
    })
  })
})

describe('remembered length', () => {
  it("uses the employee's usual length for the first suggestion", () => {
    // 09:07 in Amman + notice → 09:30, for the remembered 2h.
    expect(suggestWindow(building, new Date('2026-09-29T06:07:00Z'), 120)).toEqual({
      date: '2026-09-29',
      start: '09:30',
      end: '11:30',
    })
  })

  it("still caps a room's suggestion at the room's maximum", () => {
    const slot = suggestSlot(building, spaceWith('07:00', '20:00', 60), new Date('2026-09-29T06:07:00Z'), 180)

    expect(slot.end).toBe('10:30')
  })
})

