// The maths behind picking a time by dragging on a room's day bar. Pure functions over
// minutes-from-midnight, so the rules (snap to the grid, stay inside free time, respect the
// room's maximum) are testable without a DOM.

export interface MinuteRange {
  start: number
  end: number
}

export interface FreeTimeRules {
  /** Minutes the room is open that day. */
  open: MinuteRange[]
  /** Bookings and closures. */
  blockers: MinuteRange[]
  slotMinutes: number
  /** Nothing may start before this (today: now + notice). */
  minStart: number
  /** The room's longest allowed booking. */
  maxDuration: number
}

const floorTo = (m: number, slot: number) => Math.floor(m / slot) * slot
const ceilTo = (m: number, slot: number) => Math.ceil(m / slot) * slot

/**
 * The free stretch around `minute`: inside an open range, between the nearest blockers on
 * either side, and not before `minStart`. Null if `minute` itself isn't free.
 */
export function freeStretchAt(minute: number, rules: FreeTimeRules): MinuteRange | null {
  const open = rules.open.find((o) => o.start <= minute && minute < o.end)
  if (!open) return null
  if (rules.blockers.some((b) => b.start <= minute && minute < b.end)) return null

  let start = Math.max(open.start, ceilTo(rules.minStart, rules.slotMinutes))
  let end = open.end
  for (const b of rules.blockers) {
    if (b.end <= minute) start = Math.max(start, b.end)
    if (b.start > minute) end = Math.min(end, b.start)
  }

  return minute >= start && end - start >= rules.slotMinutes ? { start, end } : null
}

/**
 * The range a drag from `anchor` to `pointer` selects: snapped to the slot grid, at least
 * one slot long, never leaving the free stretch the drag started in, and never longer than
 * the room allows. Dragging left extends the start instead of the end.
 */
export function dragRange(anchor: number, pointer: number, stretch: MinuteRange, rules: FreeTimeRules): MinuteRange {
  const slot = rules.slotMinutes
  const a = Math.min(Math.max(floorTo(anchor, slot), stretch.start), stretch.end - slot)

  if (pointer >= a) {
    const end = Math.min(Math.max(ceilTo(pointer, slot), a + slot), stretch.end, a + rules.maxDuration)
    return { start: a, end }
  }

  const end = a + slot
  const start = Math.max(floorTo(pointer, slot), stretch.start, end - rules.maxDuration)
  return { start, end }
}

/** A single click (no drag): the usual length starting there, trimmed to the free stretch. */
export function clickRange(minute: number, length: number, stretch: MinuteRange, rules: FreeTimeRules): MinuteRange {
  const start = Math.min(Math.max(floorTo(minute, rules.slotMinutes), stretch.start), stretch.end - rules.slotMinutes)
  return { start, end: Math.min(start + Math.min(length, rules.maxDuration), stretch.end) }
}
