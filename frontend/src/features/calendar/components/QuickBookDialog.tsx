import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight, Minus, Plus, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useDebouncedValue } from '@/hooks/useDebouncedValue'
import { formatDate, fromMinutes, toLocalDateTime, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { formatDuration } from '@/features/bookings/format'
import { FromToFields } from '@/features/bookings/components/FromToFields'
import { groupUnavailable } from '@/features/calendar/unavailableGroups'
import { OwnClashNotice } from '@/features/bookings/components/OwnClashNotice'
import { ApiError, searchAvailability } from '@/features/bookings/api/bookingsApi'
import type { SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'

export interface QuickBookWindow {
  date: IsoDate
  start: number
  end: number
}

interface QuickBookDialogProps {
  token: string
  window: QuickBookWindow
  slotMinutes: number
  today: IsoDate
  /** The earliest minute that may be picked when the window's date is today (now + notice). */
  firstBookableMinute: number
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
export function QuickBookDialog({ token, window: picked, slotMinutes, today, firstBookableMinute, onClose, onPick }: QuickBookDialogProps) {
  const [attendees, setAttendees] = useState(1)
  const [w, setWindow] = useState(picked)
  const people = useDebouncedValue(attendees, 250)
  const start = fromMinutes(w.start)
  const end = fromMinutes(w.end)

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
          <DialogTitle>Book a room</DialogTitle>
          <DialogDescription>{formatDate(w.date)}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-3 sm:grid-cols-2">
          <FromToFields
            idPrefix="qb"
            start={start}
            end={end}
            slotMinutes={slotMinutes}
            minStart={w.date === today ? firstBookableMinute : 0}
            onChange={(range) => setWindow({ ...w, start: toMinutes(range.start), end: toMinutes(range.end) })}
          />
        </div>

        <div className="flex items-center justify-between gap-3">
          <span className="text-sm font-medium" id="qb-people-label">
            People
          </span>
          <div className="flex items-center gap-1" role="group" aria-labelledby="qb-people-label">
            <Button
              variant="outline"
              size="icon-sm"
              aria-label="Fewer people"
              disabled={attendees <= 1}
              onClick={() => setAttendees((n) => Math.max(1, n - 1))}
            >
              <Minus />
            </Button>
            <span className="w-8 text-center font-mono tabular-nums" aria-live="polite">
              {attendees}
            </span>
            <Button variant="outline" size="icon-sm" aria-label="More people" onClick={() => setAttendees((n) => n + 1)}>
              <Plus />
            </Button>
          </div>
        </div>

        <section aria-busy={results.isRefreshing} className="max-h-[60vh] min-h-40 overflow-y-auto pe-1">
          {results.status === 'loading' && <p className="py-6 text-center text-sm text-muted-foreground">Checking every room…</p>}

          {results.status === 'error' && (
            <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
              {results.error instanceof ApiError ? results.error.message : "Couldn't check the rooms — please try again."}
            </p>
          )}

          {results.status === 'success' && (
            <>
              <div className="mb-3 empty:hidden">
                <OwnClashNotice warnings={results.data.warnings} />
              </div>
              <p className="mb-2 text-sm font-medium" aria-live="polite">
                {free.length === 0 ? 'No room is free for this time' : `${free.length} ${free.length === 1 ? 'room' : 'rooms'} free`}
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
                            {room.floorName} · {room.space.spaceTypeName} · {room.space.capacity}{' '}
                            {room.space.capacity === 1 ? 'seat' : 'seats'}
                          </span>
                          <span className="block truncate text-xs text-muted-foreground">
                            Up to {formatDuration(room.space.maxDurationMinutes.value)}
                            {room.space.minAttendees ? ` · min ${room.space.minAttendees} people` : ''}
                          </span>
                        </span>
                        {room.freeUntil && (
                          <span className="text-xs whitespace-nowrap text-muted-foreground">
                            {room.freeUntil === '24:00' ? 'Free all day' : `Free until ${room.freeUntil}`}
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
                    {free.length === 0 ? 'Why no room is free' : `${groups.reduce((n, g) => n + g.rooms.length, 0)} other rooms can't take this time`}
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
                        <ChevronRight className="size-4 flex-none text-muted-foreground transition-transform group-open:rotate-90" />
                        <span className="flex-1">
                          <span className="font-medium">{group.title}</span>
                          <span className="text-muted-foreground">
                            {' '}
                            · {group.rooms.length} {group.rooms.length === 1 ? 'room' : 'rooms'}
                          </span>
                          {group.shortenTo && (
                            <span className="block text-xs text-[var(--state-expired-ink)]">
                              They allow up to {formatDuration(group.shortenTo)} — this is {formatDuration(w.end - w.start)}.
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
                            Shorten to {formatDuration(group.shortenTo)}
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
              <Search /> Open in Find a space
            </Link>
          </Button>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
