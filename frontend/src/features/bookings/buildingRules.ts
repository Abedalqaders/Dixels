// What any room in the building could take on a day, for pickers that aren't about one room
// yet (Find a space's search bar, the calendar's Book a room). A room can only narrow its
// building's days and hours, and none allows longer than the longest room, so these are the
// widest rules — times outside them can't be booked anywhere. Bookings and closures belong
// to rooms, so they're left to the search results.

import { addDays, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate, ZonedNow } from '@/lib/time/buildingTime'
import { weekday } from '@/features/calendar/calendarDates'
import type { BookableBuildingDto } from './api/bookingsApi'
import type { FreeTimeRules, MinuteRange } from './dragRange'
import { hasFreeTime } from './freeTimes'

const DAY_MINUTES = 24 * 60

type Building = Pick<BookableBuildingDto, 'days' | 'hours' | 'floors' | 'slotMinutes' | 'minLeadMinutes' | 'maxHorizonDays'>

/**
 * The minutes of `date` the building is open. Hours that run past midnight (22:00–06:00)
 * open in the evening and carry on into the early hours of the next day — so a day also gets
 * the morning left over from the night before, if that night was an open day.
 */
export function buildingOpenRanges(building: Pick<BookableBuildingDto, 'days' | 'hours'>, date: IsoDate): MinuteRange[] {
  const { isOpen24Hours, open, close } = building.hours
  const openOn = (d: IsoDate) => building.days.includes(weekday(d))

  if (isOpen24Hours) return openOn(date) ? [{ start: 0, end: DAY_MINUTES }] : []

  const from = toMinutes(open)
  const to = toMinutes(close)
  if (to > from) return openOn(date) ? [{ start: from, end: to }] : []

  const ranges: MinuteRange[] = []
  if (to > 0 && openOn(addDays(date, -1))) ranges.push({ start: 0, end: to })
  if (openOn(date)) ranges.push({ start: from, end: DAY_MINUTES })
  return ranges
}

/** The longest booking any room in the building allows. */
export function longestRoomLength(building: Pick<BookableBuildingDto, 'floors'>): number {
  const lengths = building.floors.flatMap((f) => f.spaces.map((s) => s.maxDurationMinutes.value))
  return lengths.length ? Math.max(...lengths) : DAY_MINUTES
}

/** The most people any room in the building seats. */
export function biggestRoom(building: Pick<BookableBuildingDto, 'floors'>): number {
  const seats = building.floors.flatMap((f) => f.spaces.map((s) => s.capacity))
  return seats.length ? Math.max(...seats) : 1
}

/** The building's day as free-time rules: open hours, not before now + notice on today, no longer than the longest room. */
export function buildingRules(building: Building, date: IsoDate, now: ZonedNow): FreeTimeRules {
  return {
    open: buildingOpenRanges(building, date),
    blockers: [],
    slotMinutes: building.slotMinutes,
    minStart: date === now.date ? now.minutes + building.minLeadMinutes : 0,
    maxDuration: longestRoomLength(building),
  }
}

/** Whether anything could be booked on `date`: within the booking window, and the building open with time left. */
export function canBookOn(building: Building, date: IsoDate, now: ZonedNow): boolean {
  return date >= now.date && date <= addDays(now.date, building.maxHorizonDays) && hasFreeTime(buildingRules(building, date, now))
}
