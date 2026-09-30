import { Search, X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { bookingPhase } from '@/features/calendar/bookingPhase'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { BookingDetails, findSpaceLink, PhaseBadge } from './BookingDetails'

interface BookingDetailPanelProps {
  item: CalendarItem
  booking: BookingDto | null
  error: string | null
  /** Whether to offer other rooms at this time — only to someone who may book them. */
  canBook: boolean
  /** Whether to offer Cancel — only to someone holding Bookings.Cancel. */
  canCancel: boolean
  onClose: () => void
  onCancel: (booking: BookingDto) => void
}

/**
 * One booking's details in the calendar's side panel, under the mini calendar — the
 * calendar stays in view while you read, and the next click just swaps the booking.
 */
export function BookingDetailPanel({ item, booking, error, canBook, canCancel, onClose, onCancel }: BookingDetailPanelProps) {
  const phase = booking ? bookingPhase(booking) : null
  const link = canBook ? findSpaceLink(item, booking) : null

  return (
    <section className="grid gap-4 border-t p-4" aria-label="Booking details" aria-busy={!booking && !error}>
      <div className="flex items-start gap-2">
        <h3 className="min-w-0 flex-1 text-base leading-tight font-semibold">{item.title}</h3>
        {phase && <PhaseBadge phase={phase} />}
        <Button variant="ghost" size="icon-xs" className="-mt-0.5 -me-1" aria-label="Close details" onClick={onClose}>
          <X />
        </Button>
      </div>

      <BookingDetails item={item} booking={booking} error={error} />

      <div className="flex flex-wrap gap-2">
        {canCancel && booking && phase === 'upcoming' && (
          <Button variant="destructive" size="sm" onClick={() => onCancel(booking)}>
            Cancel booking
          </Button>
        )}
        {link && (
          <Button variant="outline" size="sm" asChild>
            <Link to={link}>
              <Search /> Other rooms at this time
            </Link>
          </Button>
        )}
      </div>
    </section>
  )
}
