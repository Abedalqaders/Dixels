import { CircleAlert, CircleCheck, LoaderCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { PreviewState } from '@/features/bookings/hooks/useBookingPreview'
import { OwnClashNotice } from './OwnClashNotice'

interface VerdictPanelProps {
  state: PreviewState
  /** "Tue 29 Sep, 10:00–11:00" — shown when the slot is free. */
  slotLabel: string
  timezone: string
}

const BOX = 'flex gap-2.5 rounded-md px-4 py-3 text-sm [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none'

/**
 * The live answer to "can I book this?". Every broken rule is listed, most fundamental
 * first; the first is emphasised because fixing it may make the others irrelevant (no
 * point adjusting the time on a room that's closed all day). Colours come from the theme:
 * the theme hue for "free", the blocked state colour for a rejection.
 */
export function VerdictPanel({ state, slotLabel, timezone }: VerdictPanelProps) {
  const { t } = useTranslation()
  if (state.status === 'idle') {
    return null
  }

  if (state.status === 'checking') {
    return (
      <div className={`${BOX} bg-muted text-muted-foreground`} role="status" aria-live="polite">
        <LoaderCircle className="animate-spin" />
        {t('BookingForm:CheckingAvailability')}
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className={`${BOX} bg-slot-closed`} role="alert">
        <CircleAlert />
        {state.message}
      </div>
    )
  }

  const { preview } = state

  if (preview.isValid) {
    return (
      <div className="grid gap-2">
        <div className={`${BOX} bg-slot-open`} role="status" aria-live="polite">
          <CircleCheck className="text-brand" />
          <span>
            <strong>{t('BookingForm:Available')}</strong> — {slotLabel} ({timezone})
          </span>
        </div>
        <OwnClashNotice warnings={preview.warnings} />
      </div>
    )
  }

  // Not bookable, but every reason is shown under its own field (the form filters them
  // out before passing the state here): nothing left for the panel to say.
  if (preview.violations.length === 0) {
    return null
  }

  const [first, ...rest] = preview.violations

  return (
    <div className={`${BOX} bg-slot-closed`} role="alert">
      <CircleAlert />
      <div>
        <strong>{first.message}</strong>
        {rest.length > 0 && (
          <ul className="mt-2 list-disc space-y-1 ps-5">
            {rest.map((v) => (
              <li key={v.code + v.message}>{v.message}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
