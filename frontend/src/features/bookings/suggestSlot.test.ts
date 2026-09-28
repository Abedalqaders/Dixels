import { describe, expect, it } from 'vitest'
import type { BookableBuildingDto, BookableSpaceDto } from '@/features/bookings/api/bookingsApi'
import { suggestSlot, suggestWindow } from './suggestSlot'

const building = {
  timezone: 'Asia/Amman', // UTC+3
  slotMinutes: 15,
  minLeadMinutes: 15,
} as BookableBuildingDto

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

  it('moves to tomorrow morning late in the evening', () => {
    // 23:30 in Amman.
    expect(suggestWindow(building, new Date('2026-09-29T20:30:00Z'))).toEqual({
      date: '2026-09-30',
      start: '09:00',
      end: '10:00',
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

