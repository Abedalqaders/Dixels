import { describe, expect, it } from 'vitest'
import { clickRange, dragRange, freeStretchAt } from './dragRange'
import type { FreeTimeRules } from './dragRange'

const h = (hours: number, minutes = 0) => hours * 60 + minutes

// Open 07:00–20:00, booked 12:00–13:00, 15-minute grid, max 3h, nothing before 08:00.
const rules: FreeTimeRules = {
  open: [{ start: h(7), end: h(20) }],
  blockers: [{ start: h(12), end: h(13) }],
  slotMinutes: 15,
  minStart: h(8),
  maxDuration: 180,
}

describe('freeStretchAt', () => {
  it('is bounded by the booking after it and by "now" before it', () => {
    expect(freeStretchAt(h(10), rules)).toEqual({ start: h(8), end: h(12) })
  })

  it('is bounded by the booking before it and by closing time', () => {
    expect(freeStretchAt(h(15), rules)).toEqual({ start: h(13), end: h(20) })
  })

  it('is null on a booking, outside opening hours, and before the earliest start', () => {
    expect(freeStretchAt(h(12, 30), rules)).toBeNull()
    expect(freeStretchAt(h(21), rules)).toBeNull()
    expect(freeStretchAt(h(7, 30), rules)).toBeNull()
  })
})

describe('dragRange', () => {
  const stretch = { start: h(8), end: h(12) }

  it('snaps the start down and the end up to the grid', () => {
    expect(dragRange(h(9, 7), h(10, 20), stretch, rules)).toEqual({ start: h(9), end: h(10, 30) })
  })

  it('stops at the next booking instead of crossing it', () => {
    expect(dragRange(h(10), h(14), stretch, rules)).toEqual({ start: h(10), end: h(12) })
  })

  it("never exceeds the room's maximum", () => {
    expect(dragRange(h(8), h(11, 45), stretch, { ...rules, maxDuration: 60 })).toEqual({ start: h(8), end: h(9) })
  })

  it('extends the start when dragging left, down to the start of the free stretch', () => {
    expect(dragRange(h(10), h(7), stretch, rules)).toEqual({ start: h(8), end: h(10, 15) })
  })

  it('is always at least one slot long', () => {
    expect(dragRange(h(10), h(10), stretch, rules)).toEqual({ start: h(10), end: h(10, 15) })
  })
})

describe('clickRange', () => {
  it('books the usual length from the clicked slot', () => {
    expect(clickRange(h(9, 5), 60, { start: h(8), end: h(12) }, rules)).toEqual({ start: h(9), end: h(10) })
  })

  it('trims the usual length to the free time left', () => {
    expect(clickRange(h(11, 30), 60, { start: h(8), end: h(12) }, rules)).toEqual({ start: h(11, 30), end: h(12) })
  })
})
