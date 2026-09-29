import type { SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'

const CODE = 'Dixels:Bookings:'
export const TOO_LONG = `${CODE}TooLong`

// What each rule is called when it's the reason a group of rooms can't take the time.
// Keyed by the server's violation codes; anything new falls back to "Not available".
const TITLES: Record<string, string> = {
  [TOO_LONG]: 'Shorter time limit',
  [`${CODE}SpaceClosed`]: 'Closed',
  [`${CODE}Overlap`]: 'Already booked',
  [`${CODE}OverCapacity`]: 'Too small',
  [`${CODE}BelowMinAttendees`]: 'Needs more people',
  [`${CODE}ClosedDay`]: 'Not open that day',
  [`${CODE}OutsideHours`]: 'Outside opening hours',
  [`${CODE}BeyondHorizon`]: 'Too far ahead',
  [`${CODE}TooSoon`]: 'Too soon',
  [`${CODE}StartInPast`]: 'Too soon',
  [`${CODE}NotAligned`]: 'Off the time grid',
  [`${CODE}OwnOverlap`]: "You're already booked then",
}

export interface UnavailableGroup {
  code: string
  title: string
  /** Each room with the short reason the server gave for it ("Seats 1 — you need 4"). */
  rooms: { room: SpaceAvailabilityDto; reason: string }[]
  /** Too long is the only problem for these rooms — shortening to `shortenTo` minutes frees at least one. */
  shortenTo?: number
}

/**
 * Sorts the rooms that can't take a time into one group per reason, so the panel can say
 * "Already booked · 5 rooms" instead of one lump. Each room lands in exactly one group,
 * by its most fundamental problem (the server lists violations most serious first) — with
 * one exception: "too long" only counts as the reason when it's the room's *only*
 * problem, because only then does shortening help. Groups come biggest first.
 */
export function groupUnavailable(spaces: SpaceAvailabilityDto[]): UnavailableGroup[] {
  const groups = new Map<string, UnavailableGroup>()

  for (const room of spaces) {
    if (room.isAvailable || room.violations.length === 0) continue

    const onlyTooLong = room.violations.every((v) => v.code === TOO_LONG)
    const reason = onlyTooLong ? room.violations[0] : room.violations.find((v) => v.code !== TOO_LONG)!

    let group = groups.get(reason.code)
    if (!group) {
      group = { code: reason.code, title: TITLES[reason.code] ?? 'Not available', rooms: [] }
      groups.set(reason.code, group)
    }
    group.rooms.push({ room, reason: reason.shortMessage })
  }

  const tooLong = groups.get(TOO_LONG)
  if (tooLong) tooLong.shortenTo = Math.max(...tooLong.rooms.map((r) => r.room.space.maxDurationMinutes.value))

  return [...groups.values()].sort((a, b) => b.rooms.length - a.rooms.length)
}
