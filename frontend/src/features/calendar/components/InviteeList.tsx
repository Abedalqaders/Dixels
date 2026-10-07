import { useTranslation } from 'react-i18next'
import { GuestTag, Initials } from '@/components/PeoplePicker'
import type { BookingInviteeDto } from '@/features/bookings/api/bookingsApi'

/**
 * Who's invited to a booking, a row each: initials, name and — when the server sent it,
 * which it does for the owner only — the email. Guests from outside get the "Guest" tag.
 */
export function InviteeList({ invitees }: { invitees: BookingInviteeDto[] }) {
  const { t } = useTranslation()
  return (
    <ul className="grid gap-2" aria-label={t('Booking:Invitees')}>
      {invitees.map((i) => (
        <li key={i.userId ?? `guest:${i.email || i.name}`} className="flex min-w-0 items-center gap-2">
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
  )
}
