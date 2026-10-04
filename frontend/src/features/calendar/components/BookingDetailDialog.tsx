import { useTranslation } from 'react-i18next'
import { Search } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { bookingPhase } from '@/features/calendar/bookingPhase'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { BookingDetails, findSpaceLink, PhaseBadge } from './BookingDetails'

interface BookingDetailDialogProps {
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

/** One booking's details as a dialog — for screens too narrow for the calendar's side panel. */
export function BookingDetailDialog({ item, booking, error, canBook, canCancel, onClose, onCancel }: BookingDetailDialogProps) {
  const { t } = useTranslation()
  const phase = booking ? bookingPhase(booking) : null
  const link = canBook ? findSpaceLink(item, booking) : null

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md" aria-busy={!booking && !error}>
        <DialogHeader>
          <div className="flex items-center gap-2">
            <DialogTitle className="text-lg">{item.title}</DialogTitle>
            {phase && <PhaseBadge phase={phase} />}
          </div>
          <DialogDescription className="sr-only">{t('Booking:Details')}</DialogDescription>
        </DialogHeader>

        <BookingDetails item={item} booking={booking} error={error} />

        <DialogFooter className="gap-2 sm:justify-between">
          {link ? (
            <Button variant="ghost" asChild>
              <Link to={link}>
                <Search /> {t('Booking:OtherRooms')}
              </Link>
            </Button>
          ) : (
            <span />
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>
              {t('Common:Close')}
            </Button>
            {canCancel && booking && phase === 'upcoming' && (
              <Button variant="destructive" onClick={() => onCancel(booking)}>
                {t('Booking:Cancel')}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
