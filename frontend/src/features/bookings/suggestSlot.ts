import { addDays, fromMinutes, nextSlot, nowInZone, toMinutes } from '@/lib/time/buildingTime'
import type { HhMm, IsoDate } from '@/lib/time/buildingTime'
import type { BookableBuildingDto, BookableSpaceDto } from '@/features/bookings/api/bookingsApi'
import { readLastDuration } from './preferences'

const DAY_MINUTES = 24 * 60
/** Used until the employee has booked once; after that, their last booking's length. */
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
export function suggestSlot(
  building: BookableBuildingDto,
  space: BookableSpaceDto,
  now = new Date(),
  preferredLength = readLastDuration() ?? DEFAULT_LENGTH_MINUTES,
): Slot {
  const slot = building.slotMinutes
  const length = Math.min(preferredLength, space.maxDurationMinutes.value)
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

/**
 * The window a search starts with before the employee picks one: the next slot after the
 * building's minimum notice, today on the building's clock, for the employee's usual
 * length (their last booking's, else an hour) — or 09:00 tomorrow once today has no room
 * left for it.
 */
export function suggestWindow(
  building: Pick<BookableBuildingDto, 'timezone' | 'slotMinutes' | 'minLeadMinutes'>,
  now = new Date(),
  length = readLastDuration() ?? DEFAULT_LENGTH_MINUTES,
): Slot {
  const zoned = nowInZone(building.timezone, now)
  const start = nextSlot(zoned.minutes + building.minLeadMinutes, building.slotMinutes)

  if (start + length > DAY_MINUTES) {
    const morning = 9 * 60
    return { date: addDays(zoned.date, 1), start: fromMinutes(morning), end: fromMinutes(Math.min(morning + length, DAY_MINUTES)) }
  }

  return { date: zoned.date, start: fromMinutes(start), end: fromMinutes(start + length) }
}
