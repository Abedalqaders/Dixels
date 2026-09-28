import { describe, expect, it } from 'vitest'
import { bookingMinutes, hourSpan, layoutDay } from './dayLayout'

const at = (h: number, m = 0) => h * 60 + m
const item = (id: string, start: number, end: number) => ({ item: id, start, end })
const lanesOf = (placed: ReturnType<typeof layoutDay<string>>) =>
  Object.fromEntries(placed.map((p) => [p.item, `${p.lane}/${p.lanes}`]))

describe('layoutDay', () => {
  it('gives a lone booking the full width', () => {
    expect(lanesOf(layoutDay([item('a', at(9), at(10))]))).toEqual({ a: '0/1' })
  })

  it('puts overlapping bookings side by side', () => {
    expect(lanesOf(layoutDay([item('a', at(9), at(11)), item('b', at(10), at(12))]))).toEqual({ a: '0/2', b: '1/2' })
  })

  it("doesn't treat back-to-back bookings as overlapping", () => {
    expect(lanesOf(layoutDay([item('a', at(9), at(10)), item('b', at(10), at(11))]))).toEqual({ a: '0/1', b: '0/1' })
  })

  it('reuses a freed lane and keeps one lane count for a chained group', () => {
    // a overlaps b, b overlaps c, but a and c don't — c can take a's lane.
    const placed = layoutDay([item('a', at(9), at(10)), item('b', at(9, 30), at(11)), item('c', at(10), at(10, 30))])
    expect(lanesOf(placed)).toEqual({ a: '0/2', b: '1/2', c: '0/2' })
  })

  it('starts a fresh group after a gap', () => {
    const placed = layoutDay([item('a', at(9), at(11)), item('b', at(10), at(11)), item('c', at(14), at(15))])
    expect(lanesOf(placed).c).toBe('0/1')
  })
})

describe('bookingMinutes', () => {
  it('reads the local times, drawing a booking that ends at midnight to the bottom', () => {
    expect(bookingMinutes({ localStart: '2026-09-30T10:00:00', localEnd: '2026-09-30T11:30:00' }, '2026-09-30')).toEqual({ start: 600, end: 690 })
    expect(bookingMinutes({ localStart: '2026-09-30T22:00:00', localEnd: '2026-10-01T00:00:00' }, '2026-09-30')).toEqual({ start: 1320, end: 1440 })
  })
})

describe('hourSpan', () => {
  const building = (hours: { isOpen24Hours: boolean; open: string; close: string }[]) => ({
    floors: [{ id: 'f', name: 'L1', floorNumber: 1, spaces: hours.map((h, i) => ({ id: String(i), hours: { value: h, source: 'Building' } })) }],
  }) as never

  it('runs from the earliest opening to the latest closing', () => {
    const b = building([
      { isOpen24Hours: false, open: '08:00', close: '18:00' },
      { isOpen24Hours: false, open: '07:30', close: '20:00' },
    ])
    expect(hourSpan(b, [])).toEqual({ from: 7, to: 20 })
  })

  it('widens to fit a booking outside the hours, and covers the day for 24h rooms', () => {
    const b = building([{ isOpen24Hours: false, open: '08:00', close: '18:00' }])
    expect(hourSpan(b, [{ start: at(18), end: at(19, 30) }])).toEqual({ from: 8, to: 20 })
    expect(hourSpan(building([{ isOpen24Hours: true, open: '00:00', close: '00:00' }]), [])).toEqual({ from: 0, to: 24 })
  })

  it('falls back to office hours with nothing to go on', () => {
    expect(hourSpan(null, [])).toEqual({ from: 8, to: 18 })
  })
})
