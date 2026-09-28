import { Building2, Clock, Repeat, Search, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { describeRecurrence } from '@/features/bookings/recurrence'
import { bookingPhase } from '@/features/calendar/bookingPhase'
import type { BookingPhase } from '@/features/calendar/bookingPhase'

const PHASE_LABEL: Record<BookingPhase, string> = {
  upcoming: 'Upcoming',
  'in-progress': 'In progress',
  done: 'Done',
  cancelled: 'Cancelled by admin',
}

interface BookingDetailDialogProps {
  booking: BookingDto
  onClose: () => void
  onCancel: (booking: BookingDto) => void
}

/** One booking's details, with Cancel while it's still ahead — and a plain reason when it isn't. */
export function BookingDetailDialog({ booking, onClose, onCancel }: BookingDetailDialogProps) {
  const phase = bookingPhase(booking)
  const date = dateOf(booking.localStart)
  const start = timeOf(booking.localStart)
  const end = timeOf(booking.localEnd)
  const findSpaceLink = `/find-space?date=${date}&from=${start}&to=${end === '00:00' ? '24:00' : end}&people=${booking.attendees}`

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <DialogTitle className="text-lg">{booking.title}</DialogTitle>
            <Badge
              variant="secondary"
              className={cn(
                phase === 'upcoming' && 'bg-slot-open text-brand',
                phase === 'in-progress' && 'bg-[var(--state-confirmed-soft)] text-[var(--state-confirmed-ink)]',
                phase === 'cancelled' && 'bg-slot-closed text-slot-closed-ink',
              )}
            >
              {PHASE_LABEL[phase]}
            </Badge>
          </div>
          <DialogDescription className="sr-only">Booking details</DialogDescription>
        </DialogHeader>

        <dl className="grid gap-3 text-sm">
          <div className="flex items-start gap-3">
            <Clock className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
            <div>
              <dt className="sr-only">When</dt>
              <dd className="font-medium">
                {formatDate(date)} · <span className="font-mono">{start}–{end}</span>
              </dd>
              <dd className="text-muted-foreground">{booking.timezone} time</dd>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <Building2 className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
            <div>
              <dt className="sr-only">Where</dt>
              <dd className="font-medium">{booking.spaceName}</dd>
              <dd className="text-muted-foreground">
                {booking.floorName} · {booking.buildingName}
              </dd>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <Users className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
            <div>
              <dt className="sr-only">People</dt>
              <dd className="font-medium">
                {booking.attendees} {booking.attendees === 1 ? 'person' : 'people'}
              </dd>
            </div>
          </div>
          {booking.recurrence && (
            <div className="flex items-start gap-3">
              <Repeat className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
              <div>
                <dt className="sr-only">Repeats</dt>
                <dd>{describeRecurrence(booking.recurrence, date)}</dd>
              </div>
            </div>
          )}
        </dl>

        {phase === 'cancelled' ? (
          <p role="status" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
            An administrator cancelled this booking{booking.cancelReason ? ` — ${booking.cancelReason}` : ''}. The room is no
            longer held for you; pick another time or room.
          </p>
        ) : (
          phase !== 'upcoming' && (
            <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
              {phase === 'in-progress'
                ? "This booking has already started, so it can't be cancelled."
                : 'This booking is over.'}
            </p>
          )
        )}

        <DialogFooter className="gap-2 sm:justify-between">
          <Button variant="ghost" asChild>
            <Link to={findSpaceLink}>
              <Search /> Other rooms at this time
            </Link>
          </Button>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>
              Close
            </Button>
            {phase === 'upcoming' && (
              <Button variant="destructive" onClick={() => onCancel(booking)}>
                Cancel booking
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
