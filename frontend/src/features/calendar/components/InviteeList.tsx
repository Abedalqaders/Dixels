import { useTranslation } from 'react-i18next'
import { GuestTag, Initials } from '@/components/PeoplePicker'
import { cn } from '@/lib/utils'
import { InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import type { BookingInviteeDto } from '@/features/bookings/api/bookingsApi'

/** A guest's answer as a small round mark: soft green ✓, soft amber ~, soft red ✗, or a grey dashed ? while waiting. */
export function AnswerMark({ status }: { status: InviteeResponseStatus }) {
  const { t } = useTranslation()
  const [mark, label, look] =
    status === InviteeResponseStatus.Accepted
      ? ['✓', t('Booking:AnswerAccepted'), 'bg-[var(--state-confirmed-soft)] text-[var(--state-confirmed-ink)]']
      : status === InviteeResponseStatus.Declined
        ? ['✗', t('Booking:AnswerDeclined'), 'bg-[var(--state-cancelled-soft)] text-[var(--state-cancelled-ink)]']
        : status === InviteeResponseStatus.Maybe
          ? ['~', t('Booking:AnswerMaybe'), 'bg-[var(--state-expired-soft)] text-[var(--state-expired-ink)]']
          : ['?', t('Booking:AnswerNone'), 'border border-dashed border-muted-foreground/50 bg-muted text-muted-foreground']
  return (
    <span role="img" aria-label={label} title={label} className={cn('grid size-5 flex-none place-items-center rounded-full text-[11px] font-extrabold', look)}>
      {mark}
    </span>
  )
}

/** How many accepted, said maybe, declined and haven't answered yet. */
export function countAnswers(invitees: BookingInviteeDto[]) {
  const counts = { accepted: 0, maybe: 0, declined: 0, waiting: 0 }
  for (const i of invitees) {
    if (i.responseStatus === InviteeResponseStatus.Accepted) counts.accepted++
    else if (i.responseStatus === InviteeResponseStatus.Maybe) counts.maybe++
    else if (i.responseStatus === InviteeResponseStatus.Declined) counts.declined++
    else counts.waiting++
  }
  return counts
}

/**
 * Who's invited to a booking, a row each: their answer, initials, name and — when the server
 * sent it, which it does for the owner only — the email. Guests from outside get the "Guest"
 * tag. Above the rows, everyone invited sees the tally and a thin green / amber / red / grey
 * bar (the "maybe" part of the tally only when someone said Maybe).
 */
export function InviteeList({ invitees }: { invitees: BookingInviteeDto[] }) {
  const { t } = useTranslation()
  const counts = countAnswers(invitees)
  const share = (n: number) => `${(n / invitees.length) * 100}%`

  return (
    <div className="grid gap-2">
      <p className="m-0 text-xs font-medium text-muted-foreground">
        {t(counts.maybe > 0 ? 'Booking:AnswerSummaryMaybe' : 'Booking:AnswerSummary', counts)}
      </p>
      <div className="flex h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden="true">
        <span className="bg-[var(--state-confirmed-ink)]" style={{ width: share(counts.accepted) }} />
        <span className="bg-[var(--presence-maybe)]" style={{ width: share(counts.maybe) }} />
        <span className="bg-[var(--state-cancelled-ink)]" style={{ width: share(counts.declined) }} />
        <span className="bg-[var(--border-strong)]" style={{ width: share(counts.waiting) }} />
      </div>
      <ul className="m-0 grid list-none gap-2 p-0" aria-label={t('Booking:Invitees')}>
        {invitees.map((i) => (
          <li key={i.userId ?? `guest:${i.email || i.name}`} className="flex min-w-0 items-center gap-2">
            <AnswerMark status={i.responseStatus} />
            <Initials name={i.name || i.email} guest={i.isExternal} />
            <span className="grid min-w-0 flex-1">
              <span className="truncate">{i.name || i.email}</span>
              {i.email && i.name && (
                <span className="truncate text-xs text-muted-foreground" dir="ltr">
                  {i.email}
                </span>
              )}
            </span>
            {i.isExternal && <GuestTag />}
          </li>
        ))}
      </ul>
    </div>
  )
}
