import { useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { formatDate, timeOf, dateOf } from '../../../lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '../../space-management/components/spaceTypeIcons'
import { ApiError, getMyBookableBuilding } from '../api/bookingsApi'
import type { BookableFloorDto, BookableSpaceDto, BookingDto } from '../api/bookingsApi'
import { BookingForm } from '../components/BookingForm'
import { formatDays, formatDuration, formatHours } from '../format'
import '../bookings.css'

interface Selection {
  space: BookableSpaceDto
  floor: BookableFloorDto
}

/**
 * Every space in the employee's building, grouped by floor, each with its own resolved
 * rules and a Book button. A deliberately simple list for now — the availability timeline
 * with filters and drag-to-book replaces it next.
 */
export function FindSpacePage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()
  const [selection, setSelection] = useState<Selection | null>(null)

  const { status, data: building, error } = useAsync(() => getMyBookableBuilding(token), [token])

  function handleBooked(booking: BookingDto) {
    setSelection(null)
    showToast(
      `Booked ${booking.spaceName} — ${formatDate(dateOf(booking.localStart))}, ${timeOf(booking.localStart)}–${timeOf(booking.localEnd)}`,
    )
  }

  return (
    <>
      <div className="top">
        <span className="pick">
          <span className="picklbl">{building?.name ?? (status === 'loading' ? 'Loading…' : '')}</span>
        </span>
      </div>

      <div className="content">
        <h1 className="pagetitle">Find a space</h1>

        {status === 'loading' && <p className="lead">Loading spaces…</p>}

        {status === 'error' && (
          <p className="lead" role="alert">
            {error instanceof ApiError ? error.message : "Couldn't load spaces — please refresh."}
          </p>
        )}

        {status === 'success' && !building && (
          <div className="card emptystate">
            <p>You haven't been assigned to a building yet, so there's nothing to book.</p>
            <p className="muted">Ask an administrator to assign you to your building.</p>
          </div>
        )}

        {status === 'success' && building && (
          <>
            <p className="lead">
              {building.name} · times shown in {building.timezone} · book up to {building.maxHorizonDays} days ahead
            </p>

            {building.floors.length === 0 && (
              <div className="card emptystate">
                <p>This building doesn't have any spaces yet.</p>
              </div>
            )}

            {building.floors.map((floor) => (
              <FloorSection key={floor.id} floor={floor} onBook={(space) => setSelection({ space, floor })} />
            ))}
          </>
        )}
      </div>

      {selection && building && (
        <BookingForm
          token={token}
          building={building}
          space={selection.space}
          floorName={selection.floor.name}
          onClose={() => setSelection(null)}
          onBooked={handleBooked}
        />
      )}

      <Toast toast={toast} />
    </>
  )
}

function FloorSection({ floor, onBook }: { floor: BookableFloorDto; onBook: (space: BookableSpaceDto) => void }) {
  return (
    <section className="floorgroup" aria-labelledby={`floor-${floor.id}`}>
      <h2 id={`floor-${floor.id}`} className="floorname">
        {floor.name}
        <span className="count">
          {floor.spaces.length} {floor.spaces.length === 1 ? 'space' : 'spaces'}
        </span>
      </h2>
      <ul className="spacelist">
        {floor.spaces.map((space) => (
          <li key={space.id} className="spacerow card">
            <span className="spaceicon" aria-hidden="true">
              {ICONS[iconKeyToIconName(space.iconKey)]}
            </span>
            <span className="spaceinfo">
              <span className="spacename">{space.name}</span>
              <span className="spacemeta">
                {space.spaceTypeName} · {space.capacity} {space.capacity === 1 ? 'seat' : 'seats'}
                {space.minAttendees ? ` · at least ${space.minAttendees}` : ''}
              </span>
              <span className="spacerules">
                Open {formatHours(space.hours.value)}, {formatDays(space.days.value)} · up to{' '}
                {formatDuration(space.maxDurationMinutes.value)}
              </span>
            </span>
            <button type="button" className="btn sm" onClick={() => onBook(space)}>
              Book<span className="visually-hidden"> {space.name}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
