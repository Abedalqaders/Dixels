import { TriangleAlert } from 'lucide-react'
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
  return (
    <div
      role="status"
      className="mt-4 flex gap-3 rounded-lg bg-[var(--state-expired-soft)] px-4 py-3 text-sm text-[var(--state-expired-ink)] [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none"
    >
      <TriangleAlert />
      <div className="grid gap-1">
        <p className="font-semibold">{buildingName} is no longer available.</p>
        <p>
          An administrator removed it, and your upcoming bookings there were cancelled
          {onCalendar ? ' — they are shown struck through below.' : '. '}
          {!onCalendar && (
            <>
              You can still see them on{' '}
              <Link to="/my-calendar" className="font-medium underline underline-offset-4">
                My calendar
              </Link>
              .
            </>
          )}{' '}
          Ask an administrator to assign you to another building.
        </p>
      </div>
    </div>
  )
}
