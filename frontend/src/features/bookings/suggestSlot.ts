import { addDays, fromMinutes, nextSlot, nowInZone, toMinutes } from '@/lib/time/buildingTime'
import type { HhMm, IsoDate } from '@/lib/time/buildingTime'
import type { BookableBuildingDto, BookableSpaceDto } from '@/features/bookings/api/bookingsApi'
import { readLastDuration } from './preferences'
import { buildingRules, longestRoomLength } from './buildingRules'
import { firstFreeRange } from './freeTimes'

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

type WindowBuilding = Pick<
  BookableBuildingDto,
  'timezone' | 'slotMinutes' | 'minLeadMinutes' | 'maxHorizonDays' | 'days' | 'hours' | 'floors'
>

/**
 * The window a search starts with before the employee picks one: the first time the
 * building is open, from now + its minimum notice, for the employee's usual length (their
 * last booking's, else an hour) — on a later day when today has no room left for it.
 */
export function suggestWindow(building: WindowBuilding, now = new Date(), length = readLastDuration() ?? DEFAULT_LENGTH_MINUTES): Slot {
  const zoned = nowInZone(building.timezone, now)
  for (let i = 0; i <= building.maxHorizonDays; i++) {
    const date = addDays(zoned.date, i)
    const range = firstFreeRange(length, buildingRules(building, date, zoned))
    // Today's leftover scrap (a few minutes before closing) isn't a suggestion; a later day is.
    if (range && (range.end - range.start >= Math.min(length, longestRoomLength(building)) || i === building.maxHorizonDays)) {
      return { date, start: fromMinutes(range.start), end: fromMinutes(range.end) }
    }
  }

  // Shut for the whole booking window: the next slot, which the search then explains.
  const start = Math.min(nextSlot(zoned.minutes + building.minLeadMinutes, building.slotMinutes), DAY_MINUTES - building.slotMinutes)
  return { date: zoned.date, start: fromMinutes(start), end: fromMinutes(Math.min(start + length, DAY_MINUTES)) }
}

/**
 * The window offered for a day picked directly, e.g. the month view's "+": the first time
 * the building is open that day (from now + notice on today), for the employee's usual length.
 */
export function suggestWindowForDay(
  building: WindowBuilding,
  date: IsoDate,
  now = new Date(),
  length = readLastDuration() ?? DEFAULT_LENGTH_MINUTES,
): Slot {
  const zoned = nowInZone(building.timezone, now)
  const range = firstFreeRange(length, buildingRules(building, date, zoned))
  if (range) return { date, start: fromMinutes(range.start), end: fromMinutes(range.end) }

  const morning = 9 * 60
  return { date, start: fromMinutes(morning), end: fromMinutes(Math.min(morning + length, DAY_MINUTES)) }
}
