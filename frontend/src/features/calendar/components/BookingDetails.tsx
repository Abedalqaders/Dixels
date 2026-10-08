import { useTranslation } from 'react-i18next'
import { Building2, CalendarDays, Clock, Repeat, UserPlus, Users } from 'lucide-react'
import type { ComponentType, ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { dateOf, timeOf } from '@/lib/time/buildingTime'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { describeRecurrence } from '@/features/bookings/recurrence'
import { bookingPhase } from '@/features/calendar/bookingPhase'
import type { BookingPhase } from '@/features/calendar/bookingPhase'
import { rangeLabel } from '@/features/calendar/calendarDates'
import { formatClockRange } from '@/lib/time/format'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import type { TextKeys } from '@/i18n/keys'
import { Initials } from '@/components/PeoplePicker'
import { InviteeList } from './InviteeList'
import { InviteResponse } from './InviteResponse'
import type { AnswerScope } from './InviteResponse'
import { InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'

const PHASE_LABEL: Record<BookingPhase, keyof TextKeys> = {
  upcoming: 'Booking:Upcoming',
  'in-progress': 'Booking:InProgress',
  done: 'Booking:Done',
  cancelled: 'Booking:CancelledByAdmin',
}

export function PhaseBadge({ phase }: { phase: BookingPhase }) {
  const { t } = useTranslation()
  return (
    <Badge
      variant="secondary"
      className={cn(
        'flex-none',
        phase === 'upcoming' && 'bg-slot-open text-brand',
        phase === 'in-progress' && 'bg-[var(--state-confirmed-soft)] text-[var(--state-confirmed-ink)]',
        phase === 'cancelled' && 'bg-slot-closed text-slot-closed-ink',
      )}
    >
      {t(PHASE_LABEL[phase])}
    </Badge>
  )
}

/** Where to look for another room at the same time — once the head count is known. */
export function findSpaceLink(item: CalendarItem, booking: BookingDto | null): string | null {
  if (!booking) return null
  const date = dateOf(item.localStart)
  const start = timeOf(item.localStart)
  const end = timeOf(item.localEnd)
  return `/find-space?date=${date}&from=${start}&to=${end === '00:00' ? '24:00' : end}&people=${booking.attendees}`
}

export interface BookingDetailsProps {
  /** What the calendar knows already — enough for the date, time and room while the rest loads. */
  item: CalendarItem
  /** The full booking once fetched; null while it's on its way. */
  booking: BookingDto | null
  /** Why the full booking couldn't be fetched, if it couldn't. */
  error: string | null
  /** A guest's answer to the invitation (accept / decline); offered only to a guest. */
  onRespond?: (booking: BookingDto, status: InviteeResponseStatus, scope: AnswerScope) => Promise<void>
}

function Row({ icon: Icon, label, children }: { icon: ComponentType<{ className?: string }>; label: string; children: ReactNode }) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="mt-0.5 size-4 flex-none text-muted-foreground" aria-hidden="true" />
      {/* [&_dd]:m-0 — without Tailwind's preflight, dd keeps the browser's 40px indent. */}
      <div className="min-w-0 [&_dd]:m-0">
        <dt className="sr-only">{label}</dt>
        {children}
      </div>
    </div>
  )
}

/**
 * The facts about one booking, a row per icon — the same in the side panel (wide screens)
 * and the dialog (narrow ones). Shows what the calendar has at once and fills in the rest
 * as the full booking arrives, so the calendar's light list never has to carry it.
 */
export function BookingDetails({ item, booking, error, onRespond }: BookingDetailsProps) {
  const { t } = useTranslation()
  const phase = booking ? bookingPhase(booking) : null
  const date = dateOf(item.localStart)
  const invitees = booking?.invitees ?? []

  return (
    <>
      {booking && !booking.isOwner && (
        // Someone else's booking: say whose before anything else — it's why there's no Cancel.
        <p className="m-0 flex items-center gap-2.5 rounded-lg bg-slot-open px-3 py-2 text-sm">
          <Initials name={booking.ownerName} />
          <span className="min-w-0">{t('Booking:InvitedBy', { name: booking.ownerName })}</span>
        </p>
      )}
      <dl className="grid gap-3 text-sm">
        <Row icon={CalendarDays} label={t('Booking:Date')}>
          <dd className="font-medium">{rangeLabel('day', date)}</dd>
        </Row>
        <Row icon={Clock} label={t('Booking:Time')}>
          <dd className="font-medium">
            {formatClockRange(timeOf(item.localStart), timeOf(item.localEnd))}
          </dd>
          {booking ? (
            <dd className="text-muted-foreground">{t('Booking:TimezoneTime', { timezone: booking.timezone })}</dd>
          ) : (
            <Skeleton className="mt-1 h-4 w-24" />
          )}
        </Row>
        <Row icon={Building2} label={t('Booking:Where')}>
          <dd className="font-medium">{item.location}</dd>
          {booking ? (
            <dd className="text-muted-foreground">
              {booking.floorName} · {booking.buildingName}
            </dd>
          ) : (
            <Skeleton className="mt-1 h-4 w-40" />
          )}
        </Row>
        <Row icon={Users} label={t('Booking:People')}>
          {booking ? (
            <dd className="font-medium">{t('Booking:PeopleCount', { count: booking.attendees })}</dd>
          ) : (
            <Skeleton className="h-4 w-16" />
          )}
        </Row>
        {invitees.length > 0 && (
          <Row icon={UserPlus} label={t('Booking:Invitees')}>
            <dd className="font-medium">{t('Booking:InviteesCount', { count: invitees.length })}</dd>
            <dd className="mt-2">
              <InviteeList invitees={invitees} />
            </dd>
          </Row>
        )}
        {booking?.recurrence && (
          <Row icon={Repeat} label={t('Booking:Repeats')}>
            <dd>{describeRecurrence(booking.recurrence, date)}</dd>
          </Row>
        )}
      </dl>

      {booking && !booking.isOwner && onRespond && (
        <InviteResponse
          answer={booking.myResponse ?? InviteeResponseStatus.Pending}
          open={phase === 'upcoming'}
          date={date}
          isSeries={Boolean(booking.seriesId)}
          busyTimes={booking.myBusy}
          onRespond={(status, scope) => onRespond(booking, status, scope)}
        />
      )}

      {error && (
        <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
          {error}
        </p>
      )}

      {booking &&
        (phase === 'cancelled' ? (
          <p role="status" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
            {/* A guest isn't the one losing the room: they're told the organiser's booking went. */}
            {booking.isOwner
              ? booking.cancelReason
                ? t('Booking:CancelledByAdminWithReason', { reason: booking.cancelReason })
                : t('Booking:CancelledByAdminDetail')
              : booking.cancelReason
                ? t('Booking:GuestCancelledByAdminWithReason', { reason: booking.cancelReason })
                : t('Booking:GuestCancelledByAdminDetail')}
          </p>
        ) : (
          phase !== 'upcoming' && (
            <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
              {phase === 'in-progress' ? t('Booking:AlreadyStarted') : t('Booking:Over')}
            </p>
          )
        ))}
    </>
  )
}
