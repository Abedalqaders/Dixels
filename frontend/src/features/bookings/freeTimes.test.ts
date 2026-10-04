import { describe, expect, it } from 'vitest'
import type { FreeTimeRules } from './dragRange'
import { dayRules, endTimes, fitsFreeTime, hasFreeTime, nearestFreeRange, startTimes } from './freeTimes'

const h = (hours: number) => hours * 60

// Open 08:00–18:00, booked 10:00–11:00, 15-minute slots, at most 2h.
const rules: FreeTimeRules = {
  open: [{ start: h(8), end: h(18) }],
  blockers: [{ start: h(10), end: h(11) }],
  slotMinutes: 15,
  minStart: 0,
  maxDuration: 120,
}

describe('startTimes', () => {
  it('offers only free starts inside the opening hours', () => {
    const starts = startTimes(rules)
    expect(starts[0]).toBe(h(8))
    expect(starts.at(-1)).toBe(h(17) + 45)
    expect(starts).toContain(h(9) + 45)
    expect(starts).not.toContain(h(10))
    expect(starts).not.toContain(h(10) + 45)
    expect(starts).toContain(h(11))
  })

  it('starts no earlier than now + notice on today', () => {
    expect(startTimes({ ...rules, minStart: h(13) + 5 })[0]).toBe(h(13) + 15)
  })

  it('is empty on a closed day', () => {
    expect(startTimes({ ...rules, open: [] })).toEqual([])
    expect(hasFreeTime({ ...rules, open: [] })).toBe(false)
  })

  it('is empty when the whole day is booked', () => {
    expect(hasFreeTime({ ...rules, blockers: [{ start: h(8), end: h(18) }] })).toBe(false)
  })
})

describe('endTimes', () => {
  it('stops at the next booking', () => {
    expect(endTimes(h(9), rules)).toEqual([h(9) + 15, h(9) + 30, h(9) + 45, h(10)])
  })

  it("stops at the room's maximum length", () => {
    expect(endTimes(h(11), rules).at(-1)).toBe(h(13))
  })

  it('stops at closing time', () => {
    expect(endTimes(h(17), rules).at(-1)).toBe(h(18))
  })

  it('is empty for a taken start', () => {
    expect(endTimes(h(10), rules)).toEqual([])
  })
})

describe('nearestFreeRange', () => {
  it('moves a taken start to the next free one, keeping the length', () => {
    expect(nearestFreeRange(h(10), 60, rules)).toEqual({ start: h(11), end: h(12) })
  })

  it('shortens the length to fit before the next booking', () => {
    expect(nearestFreeRange(h(9) + 30, 60, rules)).toEqual({ start: h(9) + 30, end: h(10) })
  })

  it('goes back to the first free start when nothing later is free', () => {
    expect(nearestFreeRange(h(20), 60, rules)).toEqual({ start: h(8), end: h(9) })
  })

  it('is null on a day with no free time', () => {
    expect(nearestFreeRange(h(9), 60, { ...rules, open: [] })).toBeNull()
  })
})

describe('fitsFreeTime', () => {
  it('accepts a free range and refuses one over a booking', () => {
    expect(fitsFreeTime(h(8), h(9), rules)).toBe(true)
    expect(fitsFreeTime(h(9), h(11), rules)).toBe(false)
  })
})

describe('dayRules', () => {
  it('treats bookings and closures alike as in the way', () => {
    const r = dayRules(
      {
        date: '2026-10-05',
        open: [{ startMinute: h(8), endMinute: h(18), isMine: false }],
        closed: [{ startMinute: h(12), endMinute: h(13), isMine: false }],
        busy: [{ startMinute: h(10), endMinute: h(11), isMine: true }],
      },
      { slotMinutes: 15, minStart: 0, maxDuration: 120 },
    )
    expect(r.blockers).toEqual([
      { start: h(10), end: h(11) },
      { start: h(12), end: h(13) },
    ])
    expect(startTimes(r)).not.toContain(h(12))
  })
})
