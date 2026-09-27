import { useMemo, useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { useSearchParams } from 'react-router-dom'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { dateOf, formatDate, fromMinutes, timeOf, toLocalDateTime, toMinutes } from '../../../lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '../../space-management/components/spaceTypeIcons'
import { ApiError, getMyBookableBuilding, searchAvailability } from '../api/bookingsApi'
import type { BookableBuildingDto, BookingDto, SearchAvailabilityInput, SpaceAvailabilityDto } from '../api/bookingsApi'
import { BookingForm } from '../components/BookingForm'
import { DayBar } from '../components/DayBar'
import { SearchBar } from '../components/SearchBar'
import type { SearchValues } from '../components/SearchBar'
import { dayAxis } from '../dayAxis'
import { suggestWindow } from '../suggestSlot'
import '../bookings.css'

/**
 * Find a space answers one question: "what can I book for this time?". Pick when and for
 * how many people; the page lists the rooms that are free for exactly that window (Book
 * pre-fills everything), and folds the rest underneath with the reason each one can't be
 * booked and, where it's only the time, when it's free next.
 */
export function FindSpacePage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { status, data: building, error } = useAsync(() => getMyBookableBuilding(token), [token])

  return (
    <>
      <div className="top">
        <span className="pick">
          <span className="picklbl">{building?.name ?? (status === 'loading' ? 'Loading…' : '')}</span>
        </span>
      </div>

      <div className="content">
        <h1 className="pagetitle">Find a space</h1>

        {status === 'loading' && <p className="lead">Loading…</p>}

        {status === 'error' && (
          <p className="lead" role="alert">
            {error instanceof ApiError ? error.message : "Couldn't load your building — please refresh."}
          </p>
        )}

        {status === 'success' && !building && (
          <div className="card emptystate">
            <p>You haven't been assigned to a building yet, so there's nothing to book.</p>
            <p className="muted">Ask an administrator to assign you to your building.</p>
          </div>
        )}

        {status === 'success' && building && <SpaceSearch token={token} building={building} />}
      </div>
    </>
  )
}

function SpaceSearch({ token, building }: { token: string; building: BookableBuildingDto }) {
  const { toast, showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const [booking, setBooking] = useState<SpaceAvailabilityDto | null>(null)
  const [defaults] = useState(() => suggestWindow(building))

  // The search lives in the URL (?date=&from=&to=&people=&floor=&type=), so it survives a
  // refresh, the back button, and can be shared as a link.
  const values: SearchValues = {
    date: searchParams.get('date') ?? defaults.date,
    start: searchParams.get('from') ?? defaults.start,
    end: searchParams.get('to') ?? defaults.end,
    people: Math.max(1, Number(searchParams.get('people')) || 1),
    floorId: searchParams.get('floor') ?? '',
    spaceTypeId: searchParams.get('type') ?? '',
  }

  function update(patch: Partial<SearchValues>) {
    const next = { ...values, ...patch }
    const params: Record<string, string> = { date: next.date, from: next.start, to: next.end, people: String(next.people) }
    if (next.floorId) params.floor = next.floorId
    if (next.spaceTypeId) params.type = next.spaceTypeId
    setSearchParams(params, { replace: true })
  }

  const input = useMemo<SearchAvailabilityInput | null>(
    () =>
      toMinutes(values.end) > toMinutes(values.start)
        ? {
            localStart: toLocalDateTime(values.date, values.start),
            localEnd: toLocalDateTime(values.date, values.end),
            attendees: values.people,
            floorId: values.floorId || undefined,
            spaceTypeId: values.spaceTypeId || undefined,
          }
        : null,
    [values.date, values.start, values.end, values.people, values.floorId, values.spaceTypeId],
  )

  // Typing "12" in People shouldn't search for 1 and then 12.
  const debouncedInput = useDebouncedValue(input, 250)
  const results = useAsync(
    () => (debouncedInput ? searchAvailability(token, debouncedInput) : Promise.resolve(null)),
    [token, JSON.stringify(debouncedInput)],
    { keepPreviousData: true },
  )

  function handleBooked(created: BookingDto) {
    setBooking(null)
    showToast(
      `Booked ${created.spaceName} — ${formatDate(dateOf(created.localStart))}, ${timeOf(created.localStart)}–${timeOf(created.localEnd)}`,
    )
    results.refetch()
  }

  const selection = { startMinute: toMinutes(values.start), endMinute: toMinutes(values.end) }
  const spaces = results.data?.spaces ?? []
  const free = spaces.filter((s) => s.isAvailable)
  const taken = spaces.filter((s) => !s.isAvailable)
  const axis = dayAxis(spaces, selection)
  const windowLabel = `${formatDate(values.date)}, ${values.start}–${values.end}`

  return (
    <>
      <p className="lead">
        {building.name} · times in {building.timezone} · book up to {building.maxHorizonDays} days ahead
      </p>

      <SearchBar building={building} value={values} onChange={update} />

      <div className={`results${results.isRefreshing ? ' refreshing' : ''}`} aria-busy={results.status === 'loading' || results.isRefreshing}>
        {results.status === 'loading' && <p className="lead">Checking every space…</p>}

        {results.status === 'error' && (
          <div className="verdict bad" role="alert">
            {results.error instanceof ApiError ? results.error.message : "Couldn't check availability — please try again."}
          </div>
        )}

        {results.status === 'success' && results.data && (
          <>
            <h2 className="resultshead" aria-live="polite">
              {free.length > 0
                ? `${free.length} ${free.length === 1 ? 'space' : 'spaces'} free · ${windowLabel}`
                : `Nothing free · ${windowLabel}`}
            </h2>

            {free.length === 0 && (
              <p className="muted">Try another time — the spaces below show when they're free next.</p>
            )}

            {free.length > 0 && (
              <ul className="spacelist">
                {free.map((room) => (
                  <li key={room.space.id} className="resultrow card">
                    <RoomHeading room={room} />
                    <DayBar
                      axis={axis}
                      open={room.open}
                      closed={room.closed}
                      busy={room.busy}
                      selection={selection}
                      label={freeLabel(room)}
                    />
                    <span className="hint ok">{freeLabel(room)}</span>
                    <button
                      type="button"
                      className="btn sm"
                      aria-label={`Book ${room.space.name}`}
                      onClick={() => setBooking(room)}
                    >
                      Book
                    </button>
                  </li>
                ))}
              </ul>
            )}

            {taken.length > 0 && (
              <details className="unavailable" open={free.length === 0}>
                <summary>
                  {taken.length} not available at this time
                </summary>
                <ul className="spacelist">
                  {taken.map((room) => (
                    <li key={room.space.id} className="resultrow card taken">
                      <RoomHeading room={room} />
                      <DayBar
                        axis={axis}
                        open={room.open}
                        closed={room.closed}
                        busy={room.busy}
                        selection={selection}
                        label={room.violations[0]?.shortMessage ?? ''}
                      />
                      <span className="hint bad" title={room.violations.map((v) => v.message).join('\n')}>
                        {room.violations[0]?.shortMessage}
                        {room.nextFreeStart && ` · free from ${room.nextFreeStart}`}
                      </span>
                      {room.nextFreeStart ? (
                        <button
                          type="button"
                          className="btn sm sec"
                          aria-label={`Try ${room.nextFreeStart} for ${room.space.name}`}
                          onClick={() => {
                            const length = selection.endMinute - selection.startMinute
                            update({
                              start: room.nextFreeStart!,
                              end: fromMinutes(toMinutes(room.nextFreeStart!) + length),
                            })
                          }}
                        >
                          Try {room.nextFreeStart}
                        </button>
                      ) : (
                        <span />
                      )}
                    </li>
                  ))}
                </ul>
              </details>
            )}

            {spaces.length === 0 && (
              <div className="card emptystate">
                <p>No spaces match these filters.</p>
              </div>
            )}
          </>
        )}
      </div>

      {booking && (
        <BookingForm
          token={token}
          building={building}
          space={booking.space}
          floorName={booking.floorName}
          initialSlot={{ date: values.date, start: values.start, end: values.end }}
          initialAttendees={values.people}
          onClose={() => setBooking(null)}
          onBooked={handleBooked}
        />
      )}

      <Toast toast={toast} />
    </>
  )
}

function RoomHeading({ room }: { room: SpaceAvailabilityDto }) {
  const { space } = room
  return (
    <span className="roomhead">
      <span className="spaceicon" aria-hidden="true">
        {ICONS[iconKeyToIconName(space.iconKey)]}
      </span>
      <span className="spaceinfo">
        <span className="spacename">{space.name}</span>
        <span className="spacemeta">
          {room.floorName} · {space.spaceTypeName} · {space.capacity} {space.capacity === 1 ? 'seat' : 'seats'}
        </span>
      </span>
    </span>
  )
}

function freeLabel(room: SpaceAvailabilityDto): string {
  if (!room.freeUntil) return 'Free'
  return room.freeUntil === '24:00' ? 'Free the rest of the day' : `Free until ${room.freeUntil}`
}
