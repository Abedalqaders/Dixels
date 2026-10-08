import { useTranslation } from 'react-i18next'
import { FieldError } from '@/components/FieldError'
import { headCount } from '@/features/bookings/invitees'

/**
 * How many people the booking is for: you plus each guest. Nobody types it — it follows the
 * guest list, as the server counts it. A room with a minimum says before booking how many more
 * to invite; `message` is a problem with the count (too many for the room), said under it.
 */
export function HeadCount({
  id,
  guests,
  minAttendees,
  message,
}: {
  id: string
  guests: number
  minAttendees?: number | null
  message?: string | null
}) {
  const { t } = useTranslation()
  const people = headCount(guests)
  const short = minAttendees != null && people < minAttendees ? minAttendees - people : 0

  return (
    <div className="grid gap-1" role="group" aria-labelledby={`${id}-label`}>
      <span id={`${id}-label`} className="text-sm font-medium">
        {t('HeadCount:Label')}
      </span>
      <p className="text-sm" aria-describedby={message ? `${id}-error` : undefined}>
        <span className="font-mono font-medium">{t('HeadCount:People', { count: people })}</span>
        <span className="text-muted-foreground"> · {guests === 0 ? t('HeadCount:JustYou') : t('HeadCount:YouAndGuests', { count: guests })}</span>
      </p>
      {short > 0 && <p className="text-sm text-[var(--state-expired-ink)]">{t('HeadCount:InviteMore', { count: short, min: minAttendees })}</p>}
      {/* Short of the room's minimum, the hint above is the message (the preview's says the same). */}
      {message && short === 0 && <FieldError id={`${id}-error`} message={message} />}
    </div>
  )
}
