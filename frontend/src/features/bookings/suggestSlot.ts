import { addDays, fromMinutes, nextSlot, nowInZone, toMinutes } from '../../lib/time/buildingTime'
import type { HhMm, IsoDate } from '../../lib/time/buildingTime'
import type { BookableBuildingDto, BookableSpaceDto } from './api/bookingsApi'

const DAY_MINUTES = 24 * 60
const DEFAULT_LENGTH_MINUTES = 60

export interface Slot {
  date: IsoDate
  start: HhMm
  end: HhMm
}

/**
 * A sensible first suggestion: the next bookable slot on the building's clock (now + the
 * minimum notice, rounded up to the grid), no earlier than the space opens, one hour long
 * or the space's maximum if shorter. It's only a starting point — the live preview is what
 * says whether it's actually free.
 */
export function suggestSlot(building: BookableBuildingDto, space: BookableSpaceDto, now = new Date()): Slot {
  const slot = building.slotMinutes
  const length = Math.min(DEFAULT_LENGTH_MINUTES, space.maxDurationMinutes.value)
  const hours = space.hours.value
  const zoned = nowInZone(building.timezone, now)

  // Only a same-day window (open < close) gives a clean "opens at"; for 24h or overnight
  // windows any time of day can be inside the window, so no clamping.
  const open = !hours.isOpen24Hours && toMinutes(hours.close) > toMinutes(hours.open) ? toMinutes(hours.open) : 0
  const close = open > 0 ? toMinutes(hours.close) : DAY_MINUTES

  let date = zoned.date
  let start = Math.max(nextSlot(zoned.minutes + building.minLeadMinutes, slot), open)

  if (start + length > close) {
    date = addDays(date, 1)
    start = open
  }

  return { date, start: fromMinutes(start), end: fromMinutes(start + length) }
}
