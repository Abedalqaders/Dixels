import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ChevronRight, Minus, Plus, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useDebouncedValue } from '@/hooks/useDebouncedValue'
import { formatDate, fromMinutes, nowInZone, toLocalDateTime, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { formatDuration } from '@/features/bookings/format'
import { FromToFields } from '@/features/bookings/components/FromToFields'
import { groupUnavailable } from '@/features/calendar/unavailableGroups'
import { OwnClashNotice } from '@/features/bookings/components/OwnClashNotice'
import { ApiError, searchAvailability } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'
import { biggestRoom, buildingRules } from '@/features/bookings/buildingRules'

export interface QuickBookWindow {
  date: IsoDate
  start: number
  end: number
}

interface QuickBookDialogProps {
  token: string
  window: QuickBookWindow
  /** Its days, hours and rooms: From/To offer only times some room could take, People no more than the biggest seats. */
  building: BookableBuildingDto
  onClose: () => void
  /** A room was picked — the page opens the full booking form for it, for this (possibly shortened) window. */
  onPick: (room: SpaceAvailabilityDto, attendees: number, window: QuickBookWindow) => void
}

/**
 * The step after picking a time on the calendar: which rooms are actually free then, for
 * this many people — checked by the same rules a booking runs. Picking one opens the
 * booking form, pre-filled. Find
 * a space shows the full picture. Each free room shows its own limits; the rest are
 * grouped by why they can't take the time (open a group to see which rooms), and rooms
 * ruled out only by length get a one-click "Shorten to" that trims the window to fit.
 */
export function QuickBookDialog({ token, window: picked, building, onClose, onPick }: QuickBookDialogProps) {
  const { t } = useTranslation()
  const [attendees, setAttendees] = useState(1)
  const [w, setWindow] = useState(picked)
  const people = useDebouncedValue(attendees, 250)
  const start = fromMinutes(w.start)
  const end = fromMinutes(w.end)
  const now = nowInZone(building.timezone)
  const maxPeople = biggestRoom(building)

  const results = useApiQuery(
    queryKeys.bookings.search({ date: w.date, start, end, people }),
    () =>
      searchAvailability(token, {
        localStart: toLocalDateTime(w.date, start),
        localEnd: toLocalDateTime(w.date, end),
        attendees: people,
      }),
    { keepPreviousData: true },
  )

  const spaces = results.data?.spaces ?? []
  const free = spaces.filter((s) => s.isAvailable)
  const groups = groupUnavailable(spaces)
  const findSpaceLink = `/find-space?date=${w.date}&from=${start}&to=${end}&people=${attendees}`

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('Calendar:BookARoom')}</DialogTitle>
          <DialogDescription>{formatDate(w.date)}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-3 sm:grid-cols-2">
          <FromToFields
            idPrefix="qb"
            start={start}
            end={end}
            slotMinutes={building.slotMinutes}
            minStart={w.date === now.date ? now.minutes + building.minLeadMinutes : 0}
            rules={buildingRules(building, w.date, now)}
            onChange={(range) => setWindow({ ...w, start: toMinutes(range.start), end: toMinutes(range.end) })}
          />
        </div>

        <div className="flex items-center justify-between gap-3">
          <span className="text-sm font-medium" id="qb-people-label">
            {t('Booking:People')}
          </span>
          <div className="flex items-center gap-1" role="group" aria-labelledby="qb-people-label">
            <Button
              variant="outline"
              size="icon-sm"
              aria-label={t('Booking:FewerPeople')}
              disabled={attendees <= 1}
              onClick={() => setAttendees((n) => Math.max(1, n - 1))}
            >
              <Minus />
            </Button>
            <span className="w-8 text-center font-mono tabular-nums" aria-live="polite">
              {attendees}
            </span>
            <Button
              variant="outline"
              size="icon-sm"
              aria-label={t('Booking:MorePeople')}
              // No room seats more than the biggest one.
              disabled={attendees >= maxPeople}
              onClick={() => setAttendees((n) => Math.min(maxPeople, n + 1))}
            >
              <Plus />
            </Button>
          </div>
        </div>

        <section aria-busy={results.isRefreshing} className="max-h-[60vh] min-h-40 overflow-y-auto pe-1">
          {results.status === 'loading' && <p className="py-6 text-center text-sm text-muted-foreground">{t('QuickBook:Checking')}</p>}

          {results.status === 'error' && (
            <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
              {results.error instanceof ApiError ? results.error.message : t('QuickBook:CheckFailed')}
            </p>
          )}

          {results.status === 'success' && (
            <>
              <div className="mb-3 empty:hidden">
                <OwnClashNotice warnings={results.data.warnings} />
              </div>
              <p className="mb-2 text-sm font-medium" aria-live="polite">
                {free.length === 0 ? t('QuickBook:NoneFree') : t('QuickBook:RoomsFree', { count: free.length })}
              </p>
              {free.length > 0 && (
                <ul className="grid list-none gap-1.5 p-0">
                  {free.map((room) => (
                    <li key={room.space.id}>
                      <button
                        type="button"
                        className="flex w-full items-center gap-3 rounded-lg border px-3 py-2 text-start transition-colors hover:border-brand hover:bg-slot-open focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
                        onClick={() => onPick(room, attendees, w)}
                      >
                        <span
                          className="grid size-8 flex-none place-items-center rounded-md bg-accent text-accent-foreground [&_.ic]:size-4"
                          aria-hidden="true"
                        >
                          {ICONS[iconKeyToIconName(room.space.iconKey)]}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-semibold">{room.space.name}</span>
                          <span className="block truncate text-xs text-muted-foreground">
                            {room.floorName} · {room.space.spaceTypeName} · {t('Booking:Seats', { count: room.space.capacity })}
                          </span>
                          <span className="block truncate text-xs text-muted-foreground">
                            {t('QuickBook:UpTo', { duration: formatDuration(room.space.maxDurationMinutes.value) })}
                            {room.space.minAttendees ? ` · ${t('Booking:MinPeople', { count: room.space.minAttendees })}` : ''}
                          </span>
                        </span>
                        {room.freeUntil && (
                          <span className="text-xs whitespace-nowrap text-muted-foreground">
                            {room.freeUntil === '24:00' ? t('QuickBook:FreeAllDay') : t('QuickBook:FreeUntil', { time: room.freeUntil })}
                          </span>
                        )}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              {groups.length > 0 && (
                <div className="mt-3 grid gap-1.5">
                  <p className="text-xs font-medium text-muted-foreground">
                    {free.length === 0
                      ? t('QuickBook:WhyNoneFree')
                      : t('QuickBook:OthersUnavailable', { count: groups.reduce((n, g) => n + g.rooms.length, 0) })}
                  </p>
                  {groups.map((group) => (
                    <details
                      key={group.code}
                      className={cn(
                        'group rounded-md border px-3 py-2 text-sm',
                        group.shortenTo && 'border-[var(--state-expired-ink)]/30 bg-[var(--state-expired-soft)]',
                      )}
                      open={free.length === 0 && groups.length === 1}
                    >
                      <summary className="flex cursor-pointer list-none items-center gap-2 [&::-webkit-details-marker]:hidden">
                        <ChevronRight className="size-4 flex-none text-muted-foreground transition-transform rtl:-scale-x-100 ltr:group-open:rotate-90 rtl:group-open:-rotate-90" />
                        <span className="flex-1">
                          <span className="font-medium">{group.title}</span>
                          <span className="text-muted-foreground">
                            {' '}
                            · {t('QuickBook:RoomCount', { count: group.rooms.length })}
                          </span>
                          {group.shortenTo && (
                            <span className="block text-xs text-[var(--state-expired-ink)]">
                              {t('QuickBook:AllowUpTo', { max: formatDuration(group.shortenTo), length: formatDuration(w.end - w.start) })}
                            </span>
                          )}
                        </span>
                        {group.shortenTo && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="bg-card"
                            onClick={(e) => {
                              e.preventDefault()
                              setWindow({ ...w, end: w.start + group.shortenTo! })
                            }}
                          >
                            {t('QuickBook:ShortenTo', { duration: formatDuration(group.shortenTo) })}
                          </Button>
                        )}
                      </summary>
                      <ul className="mt-2 grid list-none gap-1 border-t p-0 pt-2">
                        {group.rooms.map(({ room, reason }) => (
                          <li key={room.space.id} className="flex justify-between gap-3 text-xs">
                            <span className="truncate font-medium">{room.space.name}</span>
                            <span className="text-end text-muted-foreground">{reason}</span>
                          </li>
                        ))}
                      </ul>
                    </details>
                  ))}
                </div>
              )}
            </>
          )}
        </section>

        <DialogFooter className="sm:justify-between">
          <Button variant="ghost" asChild>
            <Link to={findSpaceLink}>
              <Search /> {t('QuickBook:OpenInFindSpace')}
            </Link>
          </Button>
          <Button variant="outline" onClick={onClose}>
            {t('Common:Close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
