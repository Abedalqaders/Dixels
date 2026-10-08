import { useTranslation } from 'react-i18next'
import { Search, Users, X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { bookingPhase } from '@/features/calendar/bookingPhase'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { BookingDetails, findSpaceLink, PhaseBadge } from './BookingDetails'
import type { BookingDetailsProps } from './BookingDetails'

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
  /** A guest answering the invitation — see BookingDetails. */
  onRespond?: BookingDetailsProps['onRespond']
  /** Offers Edit guests on an upcoming booking I own — only to someone who may book. */
  onEditGuests?: (booking: BookingDto) => void
}

/**
 * One booking's details in the calendar's side panel, under the mini calendar — the
 * calendar stays in view while you read, and the next click just swaps the booking.
 */
export function BookingDetailPanel({ item, booking, error, canBook, canCancel, onClose, onCancel, onRespond, onEditGuests }: BookingDetailPanelProps) {
  const { t } = useTranslation()
  const phase = booking ? bookingPhase(booking) : null
  const link = canBook ? findSpaceLink(item, booking) : null

  return (
    <section className="grid gap-4 border-t p-4" aria-label={t('Booking:Details')} aria-busy={!booking && !error}>
      <div className="flex items-start gap-2">
        <h3 className="min-w-0 flex-1 text-base leading-tight font-semibold">{item.title}</h3>
        {phase && <PhaseBadge phase={phase} />}
        <Button variant="ghost" size="icon-xs" className="-mt-0.5 -me-1" aria-label={t('Booking:CloseDetails')} onClick={onClose}>
          <X />
        </Button>
      </div>

      <BookingDetails item={item} booking={booking} error={error} onRespond={onRespond} />

      <div className="flex flex-wrap gap-2">
        {onEditGuests && booking?.isOwner && phase === 'upcoming' && (
          <Button variant="outline" size="sm" onClick={() => onEditGuests(booking)}>
            <Users /> {t('Booking:EditGuests')}
          </Button>
        )}
        {canCancel && booking?.isOwner && phase === 'upcoming' && (
          <Button variant="destructive" size="sm" onClick={() => onCancel(booking)}>
            {t('Booking:Cancel')}
          </Button>
        )}
        {link && (
          <Button variant="outline" size="sm" asChild>
            <Link to={link}>
              <Search /> {t('Booking:OtherRooms')}
            </Link>
          </Button>
        )}
      </div>
    </section>
  )
}
