import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { useAuth } from 'react-oidc-context'
import { useSearchParams } from 'react-router-dom'
import { ChevronDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { useToast } from '@/components/Toast'
import { EmptyState, NoResults } from '@/components/EmptyState'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useDebouncedValue } from '@/hooks/useDebouncedValue'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { dateOf, formatDate, fromMinutes, nowInZone, timeOf, toLocalDateTime, toMinutes } from '@/lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { ApiError, getMyBookableBuilding, searchAvailability } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookingDto, SearchAvailabilityInput, SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'
import { BookingForm } from '@/features/bookings/components/BookingForm'
import { DayBar } from '@/features/bookings/components/DayBar'
import { FloorFilter } from '@/features/bookings/components/FloorFilter'
import { OwnClashNotice } from '@/features/bookings/components/OwnClashNotice'
import { BuildingRemovedNotice } from '@/features/bookings/components/BuildingRemovedNotice'
import { SearchBar } from '@/features/bookings/components/SearchBar'
import type { SearchValues } from '@/features/bookings/components/SearchBar'
import { dayAxis } from '@/features/bookings/dayAxis'
import { suggestWindow } from '@/features/bookings/suggestSlot'
import type { Slot } from '@/features/bookings/suggestSlot'
import { readLastDuration } from '@/features/bookings/preferences'
import { onlyTimeIssues } from '@/features/bookings/violationFields'
import type { DayBarPick } from '@/features/bookings/components/DayBar'
import { FindSpaceSkeleton, ResultsSkeleton, TextSkeleton } from '@/components/LoadingSkeletons'
import { TopBar } from '@/components/TopBar'

/**
 * Find a space answers one question: "what can I book for this time?". Pick when and for
 * how many people; the page lists the rooms that are free for exactly that window (Book
 * pre-fills everything), and folds the rest underneath with the reason each one can't be
 * booked and, where it's only the time, when it's free next.
 */
export function FindSpacePage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  // keepPreviousData: a refetch on returning to the tab swaps in the fresh building (rules,
  // floors, spaces) in place, instead of dropping back to the skeleton and losing the search.
  const { status, data: building, error } = useApiQuery(queryKeys.bookings.myBuilding(), () => getMyBookableBuilding(token), {
    keepPreviousData: true,
  })

  return (
    <TooltipProvider>
      <TopBar crumbs={[{ label: t('Nav:Bookings') }, { label: t('Nav:FindSpace') }]}>
        <span className="pick">
          <span className="picklbl">{status === 'loading' ? <TextSkeleton label={t('Common:LoadingBuilding')} /> : building?.name}</span>
        </span>
      </TopBar>

      <div className="content" data-compact-top="">
        <h1 className="pagetitle">{t('Nav:FindSpace')}</h1>

        {status === 'loading' && <FindSpaceSkeleton />}

        {status === 'error' && (
          <p className="lead" role="alert">
            {error instanceof ApiError ? error.message : t('Calendar:BuildingLoadFailed')}
          </p>
        )}

        {status === 'success' && !building && (
          <Card className="mt-6 gap-1 p-6">
            <p>{t('FindSpace:NoBuilding')}</p>
            <p className="text-sm text-muted-foreground">{t('Calendar:NoBuildingDetail')}</p>
          </Card>
        )}

        {status === 'success' && building?.isRemoved && <BuildingRemovedNotice buildingName={building.name} />}

        {status === 'success' && building && !building.isRemoved && <SpaceSearch token={token} building={building} />}
      </div>
    </TooltipProvider>
  )
}

function SpaceSearch({ token, building }: { token: string; building: BookableBuildingDto }) {
  const { t } = useTranslation()
  // The page opens with Bookings.Default; booking from it (Book, drag on a bar) needs Create.
  const canBook = usePermission(Permissions.Bookings.Create)
  const { showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const [booking, setBooking] = useState<{ room: SpaceAvailabilityDto; slot: Slot } | null>(null)
  const [defaults] = useState(() => suggestWindow(building))

  // The search lives in the URL (?date=&from=&to=&people=&type=&floor=), so it survives a
  // refresh, the back button, and can be shared as a link.
  const values: SearchValues = {
    date: searchParams.get('date') ?? defaults.date,
    start: searchParams.get('from') ?? defaults.start,
    end: searchParams.get('to') ?? defaults.end,
    people: Math.max(1, Number(searchParams.get('people')) || 1),
    spaceTypeId: searchParams.get('type') ?? '',
  }
  // The floor narrows the results on the page rather than the search: the whole building
  // is searched once, so every floor chip can show how many rooms it has free.
  const requestedFloor = searchParams.get('floor') ?? ''
  const floorId = building.floors.some((f) => f.id === requestedFloor) ? requestedFloor : ''

  function writeParams(next: SearchValues, floor: string) {
    const params: Record<string, string> = { date: next.date, from: next.start, to: next.end, people: String(next.people) }
    if (next.spaceTypeId) params.type = next.spaceTypeId
    if (floor) params.floor = floor
    setSearchParams(params, { replace: true })
  }

  function update(patch: Partial<SearchValues>) {
    writeParams({ ...values, ...patch }, floorId)
  }

  const { date, start, end, people, spaceTypeId } = values
  const input: SearchAvailabilityInput | null =
    toMinutes(end) > toMinutes(start)
      ? {
          localStart: toLocalDateTime(date, start),
          localEnd: toLocalDateTime(date, end),
          attendees: people,
          spaceTypeId: spaceTypeId || undefined,
        }
      : null

  // Debounced as a string key: equal searches compare equal, and typing "12" in People
  // doesn't search for 1 and then 12.
  const searchKey = useDebouncedValue(input ? JSON.stringify(input) : '', 250)
  // Keyed by the search itself. A booking made or cancelled anywhere invalidates every
  // bookings query (so the day bars refresh), and coming back to the tab re-reads a stale one.
  const results = useApiQuery(
    queryKeys.bookings.search(searchKey),
    () => (searchKey ? searchAvailability(token, JSON.parse(searchKey) as SearchAvailabilityInput) : Promise.resolve(null)),
    { keepPreviousData: true },
  )

  function handleBooked(created: BookingDto, count = 1) {
    setBooking(null)
    const booked = {
      space: created.spaceName,
      date: formatDate(dateOf(created.localStart)),
      start: timeOf(created.localStart),
      end: timeOf(created.localEnd),
    }
    showToast(count > 1 ? t('Calendar:BookedSeries', { ...booked, count }) : t('Calendar:Booked', booked))
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
  const allSpaces = results.data?.spaces ?? []
  const spaces = floorId ? allSpaces.filter((s) => s.floorId === floorId) : allSpaces
  const free = spaces.filter((s) => s.isAvailable)
  const taken = spaces.filter((s) => !s.isAvailable)
  const axis = dayAxis(spaces, selection)
  const windowLabel = t('BookingForm:Slot', { date: formatDate(values.date), start: values.start, end: values.end })

  return (
    <>
      <p className="lead">
        {t('FindSpace:Lead', { building: building.name, timezone: building.timezone, count: building.maxHorizonDays })}
      </p>

      <SearchBar building={building} value={values} onChange={update} />

      <section
        className={cn('mt-6 transition-opacity', results.isRefreshing && 'opacity-60')}
        aria-busy={results.status === 'loading' || results.isRefreshing}
      >
        {results.status === 'loading' && <ResultsSkeleton label={t('FindSpace:Checking')} />}

        {results.status === 'error' && (
          <p role="alert" className="rounded-md bg-slot-closed px-4 py-3 text-sm">
            {results.error instanceof ApiError ? results.error.message : t('BookingForm:AvailabilityCheckFailed')}
          </p>
        )}

        {results.status === 'success' && results.data && (
          <>
            <div className="mb-4 empty:hidden">
              <OwnClashNotice warnings={results.data.warnings} />
            </div>
            {building.floors.length > 1 && (
              <FloorFilter
                floors={building.floors}
                spaces={allSpaces}
                value={floorId}
                onChange={(floor) => writeParams(values, floor)}
              />
            )}

            <div className="flex flex-wrap items-baseline justify-between gap-3">
              <h2 className="font-[family-name:var(--font-display)] text-lg font-semibold" aria-live="polite">
                {free.length > 0
                  ? t('FindSpace:SpacesFree', { count: free.length, window: windowLabel })
                  : t('FindSpace:NothingFree', { window: windowLabel })}
              </h2>
              <Legend />
            </div>

            <p className="mt-1 text-sm text-muted-foreground">
              {!canBook
                ? t('FindSpace:AvailabilityOnly')
                : free.length === 0
                  ? t('FindSpace:TryAnotherTime')
                  : t('FindSpace:BookOrDrag')}
            </p>

            {free.length > 0 && (
              <ul className="mt-3 grid list-none gap-2 p-0">
                {free.map((room) => (
                  <li key={room.space.id}>
                    <ResultRow room={room}>
                      <DayBar
                        axis={axis}
                        open={room.open}
                        closed={room.closed}
                        busy={room.busy}
                        selection={selection}
                        label={freeLabel(room, t)}
                        pick={canBook ? pickerFor(room) : undefined}
                      />
                      <span className="text-sm font-medium">{freeLabel(room, t)}</span>
                      {canBook && (
                        <Button size="sm" aria-label={t('FindSpace:BookRoom', { space: room.space.name })} onClick={() => bookSearched(room)}>
                          {t('BookingForm:Book')}
                        </Button>
                      )}
                    </ResultRow>
                  </li>
                ))}
              </ul>
            )}

            {taken.length > 0 && (
              <Collapsible defaultOpen={free.length === 0} className="mt-6">
                <CollapsibleTrigger asChild>
                  <Button variant="ghost" className="group -ms-3 text-muted-foreground">
                    <ChevronDown className="transition-transform ltr:group-data-[state=closed]:-rotate-90 rtl:group-data-[state=closed]:rotate-90" />
                    {t('FindSpace:NotAvailable', { count: taken.length })}
                  </Button>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <ul className="mt-2 grid list-none gap-2 p-0">
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
                            pick={canBook && onlyTimeIssues(room.violations) ? pickerFor(room) : undefined}
                          />
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="cursor-help text-sm text-muted-foreground underline decoration-dotted underline-offset-4">
                                {room.violations[0]?.shortMessage}
                                {room.nextFreeStart && ` · ${t('FindSpace:FreeFrom', { time: room.nextFreeStart })}`}
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
                              aria-label={t('FindSpace:TryTimeFor', { time: room.nextFreeStart, space: room.space.name })}
                              onClick={() => tryTime(room.nextFreeStart!)}
                            >
                              {t('FindSpace:TryTime', { time: room.nextFreeStart })}
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
              <Card className="mt-4 py-0">
                {/* The date and time stay: they're what the person came to book. */}
                {people > 1 || spaceTypeId || floorId ? (
                  <NoResults onClear={() => writeParams({ ...values, people: 1, spaceTypeId: '' }, '')} />
                ) : (
                  <EmptyState icon={ICONS['meeting-room']} title={t('Hierarchy:NoSpaces')} />
                )}
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

    </>
  )
}

/** Room name/meta on the left, then the day bar, the status and the action — stacking on narrow screens. */
function ResultRow({ room, muted, children }: { room: SpaceAvailabilityDto; muted?: boolean; children: React.ReactNode }) {
  const { t } = useTranslation()
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
            {room.floorName} · {space.spaceTypeName} · {t('Booking:Seats', { count: space.capacity })}
          </div>
        </div>
      </div>
      {children}
    </Card>
  )
}

function Legend() {
  const { t } = useTranslation()
  const item = (swatch: string, label: string) => (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn('size-2.5 rounded-sm', swatch)} aria-hidden="true" />
      {label}
    </span>
  )
  return (
    <p className="flex flex-wrap gap-3 text-xs text-muted-foreground">
      {item('bg-slot-open', t('FindSpace:LegendOpen'))}
      {item('bg-slot-busy', t('FindSpace:LegendBooked'))}
      {item('bg-slot-mine', t('FindSpace:LegendYours'))}
      {item('bg-slot-closed', t('Calendar:Closed'))}
      {item('border-2 border-solid border-foreground', t('FindSpace:LegendYourTime'))}
    </p>
  )
}

function freeLabel(room: SpaceAvailabilityDto, t: TFunction): string {
  if (!room.freeUntil) return t('FindSpace:Free')
  return room.freeUntil === '24:00' ? t('FindSpace:FreeRestOfDay') : t('QuickBook:FreeUntil', { time: room.freeUntil })
}
