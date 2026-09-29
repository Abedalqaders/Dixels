import { describe, expect, it } from 'vitest'
import { itemMinutes, layoutDay, openWindow } from './dayLayout'

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

describe('itemMinutes', () => {
  it('reads the local times, drawing a booking that ends at midnight to the bottom', () => {
    expect(itemMinutes({ localStart: '2026-09-30T10:00:00', localEnd: '2026-09-30T11:30:00' }, '2026-09-30')).toEqual({ start: 600, end: 690 })
    expect(itemMinutes({ localStart: '2026-09-30T22:00:00', localEnd: '2026-10-01T00:00:00' }, '2026-09-30')).toEqual({ start: 1320, end: 1440 })
  })
})

describe('openWindow', () => {
  const week = [0, 1, 2, 3, 4, 5, 6]
  const office = { isOpen24Hours: false, open: '08:00', close: '18:00' }

  it('is the building hours on an open day, and null on a closed one', () => {
    expect(openWindow('2026-10-01', week, office)).toEqual({ from: 480, to: 1080 })
    expect(openWindow('2026-10-02', [0, 1, 2, 3, 4], office)).toBeNull() // a Friday
  })

  it('covers the whole day for a 24h building, and for hours that run past midnight', () => {
    expect(openWindow('2026-10-01', week, { isOpen24Hours: true, open: '00:00', close: '00:00' })).toEqual({ from: 0, to: 1440 })
    expect(openWindow('2026-10-01', week, { isOpen24Hours: false, open: '20:00', close: '02:00' })).toEqual({ from: 1200, to: 1440 })
  })
})
