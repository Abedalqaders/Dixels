import { TriangleAlert } from 'lucide-react'
import { Trans, useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'

interface BuildingRemovedNoticeProps {
  buildingName: string
  /** On My calendar the bookings are right there; elsewhere, point to them. */
  onCalendar?: boolean
}

/**
 * Shown to an employee whose building an admin deleted — instead of "you haven't been
 * assigned", which would be wrong: they were, and it went. Their upcoming bookings there
 * were cancelled and are still visible (struck through) on My calendar.
 */
export function BuildingRemovedNotice({ buildingName, onCalendar = false }: BuildingRemovedNoticeProps) {
  const { t } = useTranslation()
  return (
    <div
      role="status"
      className="mt-4 flex gap-3 rounded-lg bg-[var(--state-expired-soft)] px-4 py-3 text-sm text-[var(--state-expired-ink)] [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none"
    >
      <TriangleAlert />
      <div className="grid gap-1">
        <p className="font-semibold">{t('Booking:BuildingRemovedTitle', { building: buildingName })}</p>
        <p>
          {onCalendar ? (
            t('Booking:BuildingRemovedDetailCalendar')
          ) : (
            // <calendar>…</calendar> in the text becomes the link, so each language places it.
            // (Not <link>: that's an HTML void element, and the parser would drop its text.)
            <Trans
              i18nKey="Booking:BuildingRemovedDetail"
              components={{ calendar: <Link to="/my-calendar" className="font-medium underline underline-offset-4" /> }}
            />
          )}
        </p>
      </div>
    </div>
  )
}
