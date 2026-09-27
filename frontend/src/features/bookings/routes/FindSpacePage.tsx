import { useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { useSearchParams } from 'react-router-dom'
import { ChevronDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { dateOf, formatDate, fromMinutes, nowInZone, timeOf, toLocalDateTime, toMinutes } from '../../../lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '../../space-management/components/spaceTypeIcons'
import { ApiError, getMyBookableBuilding, searchAvailability } from '../api/bookingsApi'
import type { BookableBuildingDto, BookingDto, SearchAvailabilityInput, SpaceAvailabilityDto } from '../api/bookingsApi'
import { BookingForm } from '../components/BookingForm'
import { DayBar } from '../components/DayBar'
import { SearchBar } from '../components/SearchBar'
import type { SearchValues } from '../components/SearchBar'
import { dayAxis } from '../dayAxis'
import { formatDuration } from '../format'
import { suggestWindow } from '../suggestSlot'
import type { Slot } from '../suggestSlot'
import { readLastDuration } from '../preferences'
import type { DayBarPick } from '../components/DayBar'

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
    <TooltipProvider>
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
          <Card className="mt-6 gap-1 p-6">
            <p>You haven't been assigned to a building yet, so there's nothing to book.</p>
            <p className="text-sm text-muted-foreground">Ask an administrator to assign you to your building.</p>
          </Card>
        )}

        {status === 'success' && building && <SpaceSearch token={token} building={building} />}
      </div>
    </TooltipProvider>
  )
}

function SpaceSearch({ token, building }: { token: string; building: BookableBuildingDto }) {
  const { toast, showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const [booking, setBooking] = useState<{ room: SpaceAvailabilityDto; slot: Slot } | null>(null)
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

  const { date, start, end, people, floorId, spaceTypeId } = values
  const input: SearchAvailabilityInput | null =
    toMinutes(end) > toMinutes(start)
      ? {
          localStart: toLocalDateTime(date, start),
          localEnd: toLocalDateTime(date, end),
          attendees: people,
          floorId: floorId || undefined,
          spaceTypeId: spaceTypeId || undefined,
        }
      : null

  // Debounced as a string key: equal searches compare equal, and typing "12" in People
  // doesn't search for 1 and then 12.
  const searchKey = useDebouncedValue(input ? JSON.stringify(input) : '', 250)
  const results = useAsync(
    () =>
      searchKey
        ? searchAvailability(token, JSON.parse(searchKey) as SearchAvailabilityInput)
        : Promise.resolve(null),
    [token, searchKey],
    { keepPreviousData: true },
  )

  function handleBooked(created: BookingDto) {
    setBooking(null)
    showToast(
      `Booked ${created.spaceName} — ${formatDate(dateOf(created.localStart))}, ${timeOf(created.localStart)}–${timeOf(created.localEnd)}`,
    )
    results.refetch()
  }

  function tryTime(start: string) {
    const length = toMinutes(values.end) - toMinutes(values.start)
    update({ start, end: fromMinutes(toMinutes(start) + length) })
  }

  const selection = { startMinute: toMinutes(values.start), endMinute: toMinutes(values.end) }
  const searchLength = selection.endMinute - selection.startMinute
  const zonedNow = nowInZone(building.timezone)
  const minStart = values.date === zonedNow.date ? zonedNow.minutes + building.minLeadMinutes : 0

  function bookSearched(room: SpaceAvailabilityDto) {
    setBooking({ room, slot: { date: values.date, start: values.start, end: values.end } })
  }

  // Each bar is a picker: drag (or click, or ←/→ + Enter) on free time to book exactly that.
  function pickerFor(room: SpaceAvailabilityDto): DayBarPick {
    return {
      rules: {
        open: room.open.map((r) => ({ start: r.startMinute, end: r.endMinute })),
        blockers: [...room.busy, ...room.closed].map((r) => ({ start: r.startMinute, end: r.endMinute })),
        slotMinutes: building.slotMinutes,
        minStart,
        maxDuration: room.space.maxDurationMinutes.value,
      },
      defaultLength: readLastDuration() ?? searchLength,
      roomName: room.space.name,
      onPick: (range) =>
        setBooking({
          room,
          slot: { date: values.date, start: fromMinutes(range.start), end: fromMinutes(range.end) },
        }),
    }
  }
  const spaces = results.data?.spaces ?? []
  const free = spaces.filter((s) => s.isAvailable)
  const taken = spaces.filter((s) => !s.isAvailable)
  const axis = dayAxis(spaces, selection)
  const windowLabel = `${formatDate(values.date)} at ${values.start} for ${formatDuration(searchLength)}`

  return (
    <>
      <p className="lead">
        {building.name} · times in {building.timezone} · book up to {building.maxHorizonDays} days ahead
      </p>

      <SearchBar building={building} value={values} onChange={update} />

      <section
        className={cn('mt-6 transition-opacity', results.isRefreshing && 'opacity-60')}
        aria-busy={results.status === 'loading' || results.isRefreshing}
      >
        {results.status === 'loading' && <p className="lead">Checking every space…</p>}

        {results.status === 'error' && (
          <p role="alert" className="rounded-md bg-slot-closed px-4 py-3 text-sm">
            {results.error instanceof ApiError ? results.error.message : "Couldn't check availability — please try again."}
          </p>
        )}

        {results.status === 'success' && results.data && (
          <>
            <div className="flex flex-wrap items-baseline justify-between gap-3">
              <h2 className="font-[family-name:var(--font-display)] text-lg font-semibold" aria-live="polite">
                {free.length > 0
                  ? `${free.length} ${free.length === 1 ? 'space' : 'spaces'} free · ${windowLabel}`
                  : `Nothing free · ${windowLabel}`}
              </h2>
              <Legend />
            </div>

            <p className="mt-1 text-sm text-muted-foreground">
              {free.length === 0
                ? "Try another time — or drag on a room's bar below to book any free time it has."
                : "Book for this time, or drag on a room's bar to pick a different time and length."}
            </p>

            {free.length > 0 && (
              <ul className="mt-3 grid gap-2">
                {free.map((room) => (
                  <li key={room.space.id}>
                    <ResultRow room={room}>
                      <DayBar
                        axis={axis}
                        open={room.open}
                        closed={room.closed}
                        busy={room.busy}
                        selection={selection}
                        label={freeLabel(room)}
                        pick={pickerFor(room)}
                      />
                      <span className="text-sm font-medium">{freeLabel(room)}</span>
                      <Button size="sm" aria-label={`Book ${room.space.name}`} onClick={() => bookSearched(room)}>
                        Book
                      </Button>
                    </ResultRow>
                  </li>
                ))}
              </ul>
            )}

            {taken.length > 0 && (
              <Collapsible defaultOpen={free.length === 0} className="mt-6">
                <CollapsibleTrigger asChild>
                  <Button variant="ghost" className="group -ml-3 text-muted-foreground">
                    <ChevronDown className="transition-transform group-data-[state=closed]:-rotate-90" />
                    {taken.length} not available at this time
                  </Button>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <ul className="mt-2 grid gap-2">
                    {taken.map((room) => (
                      <li key={room.space.id}>
                        <ResultRow room={room} muted>
                          <DayBar
                            axis={axis}
                            open={room.open}
                            closed={room.closed}
                            busy={room.busy}
                            selection={selection}
                            label={room.violations[0]?.shortMessage ?? ''}
                            pick={pickerFor(room)}
                          />
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="cursor-help text-sm text-muted-foreground underline decoration-dotted underline-offset-4">
                                {room.violations[0]?.shortMessage}
                                {room.nextFreeStart && ` · free from ${room.nextFreeStart}`}
                              </span>
                            </TooltipTrigger>
                            <TooltipContent className="max-w-xs">
                              {room.violations.map((v) => (
                                <p key={v.code + v.message}>{v.message}</p>
                              ))}
                            </TooltipContent>
                          </Tooltip>
                          {room.nextFreeStart ? (
                            <Button
                              size="sm"
                              variant="outline"
                              aria-label={`Try ${room.nextFreeStart} for ${room.space.name}`}
                              onClick={() => tryTime(room.nextFreeStart!)}
                            >
                              Try {room.nextFreeStart}
                            </Button>
                          ) : (
                            <span />
                          )}
                        </ResultRow>
                      </li>
                    ))}
                  </ul>
                </CollapsibleContent>
              </Collapsible>
            )}

            {spaces.length === 0 && (
              <Card className="mt-4 p-6">
                <p>No spaces match these filters.</p>
              </Card>
            )}
          </>
        )}
      </section>

      {booking && (
        <BookingForm
          token={token}
          building={building}
          space={booking.room.space}
          floorName={booking.room.floorName}
          initialSlot={booking.slot}
          initialAttendees={values.people}
          onClose={() => setBooking(null)}
          onBooked={handleBooked}
        />
      )}

      <Toast toast={toast} />
    </>
  )
}

/** Room name/meta on the left, then the day bar, the status and the action — stacking on narrow screens. */
function ResultRow({ room, muted, children }: { room: SpaceAvailabilityDto; muted?: boolean; children: React.ReactNode }) {
  const { space } = room
  return (
    <Card
      className={cn(
        'grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 px-4 pt-5 pb-3',
        'lg:grid-cols-[minmax(200px,1.1fr)_2fr_minmax(150px,0.8fr)_auto]',
        '[&>[data-daybar]]:col-span-2 lg:[&>[data-daybar]]:col-span-1',
        muted && 'bg-muted shadow-none',
      )}
    >
      <div className="flex min-w-0 items-center gap-3">
        <span className="grid size-9 flex-none place-items-center rounded-md bg-accent text-accent-foreground [&_.ic]:size-5" aria-hidden="true">
          {ICONS[iconKeyToIconName(space.iconKey)]}
        </span>
        <div className="min-w-0">
          <div className="truncate font-semibold">{space.name}</div>
          <div className="truncate text-sm text-muted-foreground">
            {room.floorName} · {space.spaceTypeName} · {space.capacity} {space.capacity === 1 ? 'seat' : 'seats'}
          </div>
        </div>
      </div>
      {children}
    </Card>
  )
}

function Legend() {
  const item = (swatch: string, label: string) => (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn('size-2.5 rounded-sm', swatch)} aria-hidden="true" />
      {label}
    </span>
  )
  return (
    <p className="flex flex-wrap gap-3 text-xs text-muted-foreground">
      {item('bg-slot-open', 'Open')}
      {item('bg-slot-busy', 'Booked')}
      {item('bg-slot-mine', 'Yours')}
      {item('bg-slot-closed', 'Closed')}
      {item('border-2 border-solid border-foreground', 'Your time')}
    </p>
  )
}

function freeLabel(room: SpaceAvailabilityDto): string {
  if (!room.freeUntil) return 'Free'
  return room.freeUntil === '24:00' ? 'Free the rest of the day' : `Free until ${room.freeUntil}`
}
