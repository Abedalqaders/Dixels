import type { BookableBuildingDto } from '../bookings/api/bookingsApi'
import { formatDuration } from '../bookings/format'

/**
 * Every room's maximum booking length in the building, sorted — built once when the
 * building loads, so a drag can ask "how many rooms allow this long?" with a binary search
 * on each pointer move instead of scanning every room.
 */
export interface DurationLimits {
  /** Each room's max length in minutes, ascending. */
  sorted: number[]
  /** The longest any room allows — nothing longer can be booked anywhere. Infinity with no rooms. */
  longest: number
}

export function buildDurationLimits(building: Pick<BookableBuildingDto, 'floors'>): DurationLimits {
  const sorted = building.floors
    .flatMap((f) => f.spaces.map((s) => s.maxDurationMinutes.value))
    .sort((a, b) => a - b)
  return { sorted, longest: sorted.length ? sorted[sorted.length - 1] : Infinity }
}

/** How many rooms allow a booking of `minutes` — the ones whose max is at least that. */
export function roomsThatFit(limits: DurationLimits, minutes: number): number {
  // First index whose max is >= minutes; everything from there on fits.
  let lo = 0
  let hi = limits.sorted.length
  while (lo < hi) {
    const mid = (lo + hi) >> 1
    if (limits.sorted[mid] < minutes) lo = mid + 1
    else hi = mid
  }
  return limits.sorted.length - lo
}

export interface DragHint {
  /** The drag has reached the longest length any room allows. */
  capped: boolean
  /** Shown after the times on the drag box — only at the cap, so normal drags stay clean. */
  suffix: string
  /** A sentence for screen readers, or '' when there's nothing to say. */
  announcement: string
}

/**
 * What the drag box says about length: nothing until it reaches the longest any room
 * allows, then "3h max" (the drag stops there). Which rooms fit a shorter time is shown
 * after the click, in the Book a room panel, where there's room to explain it.
 */
export function dragHint(length: number, limits: DurationLimits): DragHint {
  const total = limits.sorted.length
  if (total === 0) return { capped: false, suffix: '', announcement: '' }

  if (length >= limits.longest) {
    const max = formatDuration(limits.longest)
    const fit = roomsThatFit(limits, length)
    return {
      capped: true,
      suffix: `${max} max`,
      announcement: `Capped at ${max}, the longest any room allows${fit < total ? ` — ${fit} of ${total} rooms` : ''}.`,
    }
  }

  return { capped: false, suffix: '', announcement: '' }
}
