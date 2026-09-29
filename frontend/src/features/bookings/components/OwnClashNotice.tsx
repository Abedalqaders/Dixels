import { TriangleAlert } from 'lucide-react'
import type { BookingViolationDto } from '@/features/bookings/api/bookingsApi'

/**
 * "Heads-up: you already have Desk 7 booked Tue 29 Sep 10:00–12:00." — shown wherever a
 * time is picked (the booking form, Book a room, Find a space) when the building warns
 * about holding two bookings at once. Nothing when there's nothing to say.
 */
export function OwnClashNotice({ warnings }: { warnings: BookingViolationDto[] | undefined }) {
  if (!warnings?.length) return null
  return (
    <div
      role="status"
      className="flex gap-2.5 rounded-md bg-[var(--state-expired-soft)] px-4 py-3 text-sm text-[var(--state-expired-ink)] [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none"
    >
      <TriangleAlert />
      <div className="grid gap-1">
        {warnings.map((w) => (
          <p key={w.code + w.message}>{w.message}</p>
        ))}
      </div>
    </div>
  )
}
