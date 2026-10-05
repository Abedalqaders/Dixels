import type { BookingSummaryDto } from '@/features/bookings/api/bookingsApi'

/**
 * One thing on My calendar. The grids draw these and nothing else, so what a booking is
 * (or, later, a meeting or a day off) stays out of the calendar's code: a new kind is a
 * new adapter below plus its own detail dialog, not a change to the grids.
 */
export interface CalendarItem {
  id: string
  kind: 'booking'
  title: string
  /** Wall-clock times in the building's timezone ("YYYY-MM-DDTHH:mm:ss") — what the grids place by. */
  localStart: string
  localEnd: string
  /** A short "where" for the block — the room's name. The week and day grids lead with it. */
  location: string
  /** The person's own name for it, when they gave one (not the default title). */
  note?: string
  /** Still shown, struck through, so the person knows it went. */
  cancelled: boolean
  /** One date of a recurring series. */
  repeats: boolean
}

/** What the backend names a booking left untitled (BookingConsts.DefaultTitle). */
const DEFAULT_BOOKING_TITLE = 'Booking'

/** A booking, as the calendar's light list describes it. */
export function bookingItem(b: BookingSummaryDto): CalendarItem {
  return {
    id: b.id,
    kind: 'booking',
    title: b.title,
    localStart: b.localStart,
    localEnd: b.localEnd,
    location: b.spaceName,
    note: b.title === DEFAULT_BOOKING_TITLE ? undefined : b.title,
    cancelled: b.status === 'Cancelled',
    repeats: Boolean(b.seriesId),
  }
}
