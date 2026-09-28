import type { BookingDto } from '@/features/bookings/api/bookingsApi'

export type BookingPhase = 'upcoming' | 'in-progress' | 'done'

/** Where a booking is in its life right now — from the UTC instants, so no timezone math. */
export function bookingPhase(booking: Pick<BookingDto, 'startsAt' | 'endsAt'>, now = new Date()): BookingPhase {
  if (new Date(booking.endsAt) <= now) return 'done'
  if (new Date(booking.startsAt) <= now) return 'in-progress'
  return 'upcoming'
}
