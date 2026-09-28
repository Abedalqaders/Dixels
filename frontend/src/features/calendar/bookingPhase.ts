import type { BookingDto } from '@/features/bookings/api/bookingsApi'

export type BookingPhase = 'upcoming' | 'in-progress' | 'done' | 'cancelled'

/** A booking an admin cancelled — the calendar still shows it, struck through, so the person knows why it went. */
export function isCancelled(booking: Pick<BookingDto, 'status'>): boolean {
  return booking.status === 'Cancelled'
}

/** Where a booking is in its life right now — from the UTC instants, so no timezone math. */
export function bookingPhase(booking: Pick<BookingDto, 'startsAt' | 'endsAt' | 'status'>, now = new Date()): BookingPhase {
  if (isCancelled(booking)) return 'cancelled'
  if (new Date(booking.endsAt) <= now) return 'done'
  if (new Date(booking.startsAt) <= now) return 'in-progress'
  return 'upcoming'
}
