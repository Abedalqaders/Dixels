import { useEffect, useMemo, useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { useSearchParams } from 'react-router-dom'
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { useToast } from '@/components/Toast'
import { TextSkeleton } from '@/components/LoadingSkeletons'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { addDays, dateOf, formatDate, fromMinutes, nextSlot, nowInZone, timeOf, toMinutes } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { ApiError, getBooking, getMyBookableBuilding } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookingDto, SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'
import { emitBookingsChanged, useBookingsChanged } from '@/features/bookings/bookingEvents'
import { BookingForm } from '@/features/bookings/components/BookingForm'
import { BuildingRemovedNotice } from '@/features/bookings/components/BuildingRemovedNotice'
import { readLastDuration } from '@/features/bookings/preferences'
import { suggestWindow, suggestWindowForDay } from '@/features/bookings/suggestSlot'
import {
  dayOfMonth,
  daysBetween,
  daysInMonth,
  gridMonthFor,
  isValidIsoDate,
  monthGrid,
  monthName,
  rangeLabel,
  shiftDate,
  startOfMonth,
  visibleRange,
  weekdayName,
} from '@/features/calendar/calendarDates'
import { useMonthBookings, useMonthBookingsCache } from '@/features/calendar/hooks/useMonthBookings'
import type { Prefetch } from '@/features/calendar/hooks/useMonthBookings'
import type { CalendarView } from '@/features/calendar/calendarDates'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { buildDurationLimits } from '@/features/calendar/durationLimits'
import { BookingDetailDialog } from '@/features/calendar/components/BookingDetailDialog'
import { BookingDetailPanel } from '@/features/calendar/components/BookingDetailPanel'
import { CancelBookingDialog } from '@/features/calendar/components/CancelBookingDialog'
import { MiniCalendar } from '@/features/calendar/components/MiniCalendar'
import { MonthGrid } from '@/features/calendar/components/MonthGrid'
import { QuickBookDialog } from '@/features/calendar/components/QuickBookDialog'
import type { QuickBookWindow } from '@/features/calendar/components/QuickBookDialog'
import { TimeGrid } from '@/features/calendar/components/TimeGrid'

const VIEW_KEY = 'dixels.calendar.view'
const VIEWS: CalendarView[] = ['day', 'week', 'month']

function readView(): CalendarView {
  try {
    const v = globalThis.localStorage?.getItem(VIEW_KEY)
    return v === 'day' || v === 'month' ? v : 'week'
  } catch {
    return 'week'
  }
}

function rememberView(view: CalendarView) {
  try {
    globalThis.localStorage?.setItem(VIEW_KEY, view)
  } catch {
    // Storage unavailable — the URL still carries the view.
  }
}

/** The building's clock, re-read every 30 seconds so the "now" line moves on its own. */
function useZonedNow(timeZone: string) {
  const [clock, setClock] = useState(() => ({ timeZone, now: nowInZone(timeZone) }))
  // A different building clock: re-read at once (during render, not in an effect).
  if (clock.timeZone !== timeZone) setClock({ timeZone, now: nowInZone(timeZone) })
  useEffect(() => {
    const timer = setInterval(() => setClock({ timeZone, now: nowInZone(timeZone) }), 30_000)
    return () => clearInterval(timer)
  }, [timeZone])
  return clock.now
}

/**
 * My calendar: every booking I've made, on the building's clock, as a Day, Week or Month.
 * Click a booking to see it or cancel it; drag across empty time (or click it) to see
 * which rooms are free then and book one, without leaving the page.
 */
export function MyCalendarPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { status, data: building, error } = useApiQuery(queryKeys.bookings.myBuilding(), () => getMyBookableBuilding(token))

  return (
    <>
      <div className="top">
        <span className="pick">
          <span className="picklbl">
            {status === 'loading' ? <TextSkeleton label="Loading your building…" /> : building?.name}
          </span>
        </span>
      </div>

      <div className="content" data-compact-top="">
        <h1 className="pagetitle">My calendar</h1>

        {status === 'error' && (
          <p className="lead" role="alert">
            {error instanceof ApiError ? error.message : "Couldn't load your building — please refresh."}
          </p>
        )}

        {status === 'success' && !building && (
          <Card className="mt-6 gap-1 p-6">
            <p>You haven't been assigned to a building yet, so there's nothing to show.</p>
            <p className="text-sm text-muted-foreground">Ask an administrator to assign you to your building.</p>
          </Card>
        )}

        {status === 'success' && building?.isRemoved && <BuildingRemovedNotice buildingName={building.name} onCalendar />}

        {status === 'success' && building && <Calendar token={token} building={building} />}
      </div>
    </>
  )
}

function Calendar({ token, building }: { token: string; building: BookableBuildingDto }) {
  // Bookings are still shown, but nothing can be booked from here when the building was deleted
  // (past and cancelled ones remain) or the user hasn't been granted Bookings.Create.
  const canCreate = usePermission(Permissions.Bookings.Create)
  const canCancel = usePermission(Permissions.Bookings.Cancel)
  const readOnly = Boolean(building.isRemoved) || !canCreate
  const { showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const now = useZonedNow(building.timezone)
  const wide = useMediaQuery('(min-width: 860px)')
  // Room for the side panel (mini calendar + a booking's details); below it, details open
  // in a dialog. Falls back to the dialog where there's no matchMedia to ask.
  const hasPanel = useMediaQuery('(min-width: 1024px)', false)

  // ?view=day|week|month&date=YYYY-MM-DD — a refresh or a shared link lands on the same
  // page. The view falls back to the one used last; phones only get the Day view.
  const requestedView = searchParams.get('view')
  const chosenView: CalendarView = VIEWS.includes(requestedView as CalendarView) ? (requestedView as CalendarView) : readView()
  // Phones get Day and a compact Month; Week needs the width.
  const view: CalendarView = wide ? chosenView : chosenView === 'month' ? 'month' : 'day'
  const requestedDate = searchParams.get('date')
  const date: IsoDate = isValidIsoDate(requestedDate) ? requestedDate : now.date

  function go(next: { view?: CalendarView; date?: IsoDate }) {
    const v = next.view ?? chosenView
    if (next.view) rememberView(next.view)
    setSearchParams({ view: v, date: next.date ?? date }, { replace: true })
  }

  const [miniMonth, setMiniMonth] = useState(date)
  // The mini calendar follows the main date whenever that changes (but can be browsed freely in between).
  const [miniFollows, setMiniFollows] = useState(date)
  if (miniFollows !== date) {
    setMiniFollows(date)
    setMiniMonth(date)
  }

  // One request per month grid, cached — the week, day and mini calendar all read from
  // it, so moving around is instant. The month next door is fetched ahead only when a
  // step would reach it: always in Month view, otherwise within a week of the grid's edge.
  const range = visibleRange(view, date)
  const gridMonth = gridMonthFor(view, date)
  const grid = monthGrid(gridMonth)
  const gridEnd = addDays(grid.start, grid.weeks * 7)
  const nearStart = range.from < addDays(grid.start, 7)
  const nearEnd = range.to > addDays(gridEnd, -7)
  const prefetch: Prefetch = view === 'month' || (nearStart && nearEnd) ? 'both' : nearStart ? 'prev' : nearEnd ? 'next' : 'none'
  const { cache, version, refresh } = useMonthBookingsCache(token)
  const bookings = useMonthBookings(cache, gridMonth, version, prefetch)
  const miniBookings = useMonthBookings(cache, startOfMonth(miniMonth), version)

  // Made here, made in Find a space, or cancelled: everything cached may be out of date.
  useBookingsChanged(refresh)

  // The calendar holds only the light list; opening an item fetches the booking in full.
  const [detail, setDetail] = useState<CalendarItem | null>(null)
  const detailBooking = useApiQuery(queryKeys.bookings.detail(detail?.id ?? null), () =>
    detail ? getBooking(token, detail.id) : Promise.resolve(null),
  )
  const detailError =
    detailBooking.status === 'error'
      ? detailBooking.error instanceof ApiError
        ? detailBooking.error.message
        : "Couldn't load this booking — please try again."
      : null
  const [cancelling, setCancelling] = useState<BookingDto | null>(null)

  // The heading: the day itself in Day view, otherwise the month with the exact range under it.
  const year = date.slice(0, 4)
  const heading =
    view === 'day'
      ? { big: `${monthName(date)} ${dayOfMonth(date)}, ${year}`, small: weekdayName(date) }
      : view === 'week'
        ? { big: `${monthName(date)} ${year}`, small: rangeLabel('week', date) }
        : { big: `${monthName(date)} ${year}`, small: `1 – ${daysInMonth(date)} ${monthName(date).slice(0, 3)} ${year}` }
  const [quickBook, setQuickBook] = useState<QuickBookWindow | null>(null)
  const [form, setForm] = useState<{ room: SpaceAvailabilityDto; window: QuickBookWindow; attendees: number } | null>(null)
  const anyDialog = Boolean(detail || cancelling || quickBook || form)

  // T = today, ←/→ = back/forward, D/W/M = view. Not while typing or in a dialog.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (anyDialog || e.metaKey || e.ctrlKey || e.altKey) return
      const target = e.target as HTMLElement | null
      if (target?.closest('input, textarea, select, [contenteditable="true"], [role="dialog"], [role="listbox"]')) return

      const key = e.key.toLowerCase()
      if (key === 't') go({ date: now.date })
      else if (e.key === 'ArrowLeft') go({ date: shiftDate(view, date, -1) })
      else if (e.key === 'ArrowRight') go({ date: shiftDate(view, date, 1) })
      else if (key === 'd') go({ view: 'day' })
      else if (wide && key === 'w') go({ view: 'week' })
      else if (key === 'm') go({ view: 'month' })
      else return
      e.preventDefault()
    }
    globalThis.addEventListener('keydown', onKey)
    return () => globalThis.removeEventListener('keydown', onKey)
  })

  // The month grid's bookings, narrowed to the days on screen.
  const items = useMemo(
    () => (bookings.data ?? []).filter((b) => b.localEnd > `${range.from}T00:00:00` && b.localStart < range.to),
    [bookings.data, range.from, range.to],
  )
  const days = daysBetween(range.from, range.to)
  // Every room's max length, sorted once per building — the drag checks it on each move.
  const limits = useMemo(() => buildDurationLimits(building), [building])
  const defaultLength = Math.min(readLastDuration() ?? 60, limits.longest)
  const firstBookableMinute = nextSlot(now.minutes + building.minLeadMinutes, building.slotMinutes)

  function newBooking() {
    const w = suggestWindow(building)
    setQuickBook({ date: w.date, start: toMinutes(w.start), end: toMinutes(w.end) })
  }

  function quickBookDay(d: IsoDate) {
    const w = suggestWindowForDay(building, d)
    setQuickBook({ date: w.date, start: toMinutes(w.start), end: toMinutes(w.end) })
  }

  function handleBooked(created: BookingDto, count = 1) {
    setForm(null)
    showToast(
      count > 1
        ? `Booked ${created.spaceName} on ${count} dates from ${formatDate(dateOf(created.localStart))}, ${timeOf(created.localStart)}–${timeOf(created.localEnd)}`
        : `Booked ${created.spaceName} — ${formatDate(dateOf(created.localStart))}, ${timeOf(created.localStart)}–${timeOf(created.localEnd)}`,
    )
    // Jump to the new booking if it's off screen.
    const day = dateOf(created.localStart)
    if (day < range.from || day >= range.to) go({ date: day })
  }

  function handleCancelled(all: BookingDto[]) {
    const cancelled = all[0]
    setCancelling(null)
    setDetail(null)
    emitBookingsChanged()
    showToast(
      all.length > 1
        ? `Cancelled ${all.length} bookings of “${cancelled.title}” — ${cancelled.spaceName} is free again then`
        : `Cancelled — ${cancelled.spaceName} is free again for ${timeOf(cancelled.localStart)}–${timeOf(cancelled.localEnd)}`,
    )
  }

  return (
    <>
      <Card className="gap-0 overflow-hidden py-0" data-calendar="">
        {/* Where you are, then the controls: date tile, month + range, ‹ Today ›, the view, New booking. */}
        <div className="flex flex-wrap items-center gap-3 border-b px-4 py-3">
          <div
            className="grid h-12 w-12 flex-none place-items-center rounded-lg border bg-card leading-none shadow-xs"
            aria-hidden="true"
          >
            <span className="text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">{monthName(date).slice(0, 3)}</span>
            <span className="text-lg font-semibold">{dayOfMonth(date)}</span>
          </div>
          {/* Month on top, the exact range under it — in reading order the range comes first. */}
          <h2 className="flex flex-col-reverse" aria-live="polite">
            <span className="text-sm text-muted-foreground">{heading.small}</span>
            <span className="font-[family-name:var(--font-display)] text-lg leading-tight font-semibold">{heading.big}</span>
          </h2>

          <span className="flex-1" />

          <span className="hidden text-sm text-muted-foreground xl:inline">
            {building.name} · {building.timezone}
          </span>
          <div className="flex items-center overflow-hidden rounded-md border">
            <Button variant="ghost" size="icon" className="rounded-none" aria-label="Previous" title="Previous (←)" onClick={() => go({ date: shiftDate(view, date, -1) })}>
              <ChevronLeft />
            </Button>
            <Button variant="ghost" className="rounded-none border-x px-3" onClick={() => go({ date: now.date })} title="Today (T)">
              Today
            </Button>
            <Button variant="ghost" size="icon" className="rounded-none" aria-label="Next" title="Next (→)" onClick={() => go({ date: shiftDate(view, date, 1) })}>
              <ChevronRight />
            </Button>
          </div>
          <Select value={view} onValueChange={(v) => v && go({ view: v as CalendarView })}>
            <SelectTrigger className="w-32" aria-label="View">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="day">Day view</SelectItem>
              {wide && <SelectItem value="week">Week view</SelectItem>}
              <SelectItem value="month">Month view</SelectItem>
            </SelectContent>
          </Select>
          {!readOnly && (
            <Button onClick={newBooking}>
              <Plus /> New booking
            </Button>
          )}
        </div>

        <div className="grid lg:grid-cols-[minmax(0,1fr)_300px]">
          <section
            className={cn('min-w-0 transition-opacity', bookings.isRefreshing && bookings.status === 'success' && 'opacity-70')}
            aria-busy={bookings.isRefreshing}
          >
            {bookings.status === 'error' && (
              <p role="alert" className="m-3 rounded-md bg-slot-closed px-4 py-3 text-sm text-slot-closed-ink">
                {bookings.error instanceof ApiError ? bookings.error.message : "Couldn't load your bookings — please try again."}
              </p>
            )}

            {view === 'month' ? (
              <MonthGrid
                date={date}
                items={items}
                today={now.date}
                compact={!wide}
                onOpenItem={setDetail}
                onOpenDay={(d) => go({ view: 'day', date: d })}
                onQuickBook={readOnly ? undefined : quickBookDay}
              />
            ) : (
              <TimeGrid
                days={days}
                items={items}
                openDays={building.days}
                openHours={building.hours}
                today={now.date}
                nowMinute={now.minutes}
                firstBookableMinute={firstBookableMinute}
                leadMinutes={building.minLeadMinutes}
                slotMinutes={building.slotMinutes}
                defaultLength={defaultLength}
                limits={limits}
                onOpenItem={setDetail}
                onPickRange={setQuickBook}
                readOnly={readOnly}
                onOpenDay={view === 'week' ? (d) => go({ view: 'day', date: d }) : undefined}
              />
            )}

            {bookings.status === 'success' && items.length === 0 && (
              <p className="border-t px-4 py-3 text-center text-sm text-muted-foreground">
                Nothing booked {view === 'day' ? 'this day' : view === 'week' ? 'this week' : 'this month'}.{' '}
                {!readOnly && (
                  <button type="button" className="font-medium text-foreground underline underline-offset-4" onClick={newBooking}>
                    Book a room
                  </button>
                )}
              </p>
            )}
          </section>

          {/* The side panel: a month to jump around, and the booking you clicked underneath. */}
          <aside className="hidden flex-col border-l lg:flex">
            <div className="p-3">
              <MiniCalendar
                selected={date}
                month={miniMonth}
                today={now.date}
                bookedDays={[...new Set((miniBookings.data ?? []).filter((b) => !b.cancelled).map((b) => dateOf(b.localStart)))]}
                onSelect={(d) => go({ date: d })}
                onMonthChange={setMiniMonth}
              />
            </div>
            {detail && hasPanel && (
              <BookingDetailPanel
                item={detail}
                booking={detailBooking.data ?? null}
                error={detailError}
                canBook={canCreate}
                canCancel={canCancel}
                onClose={() => setDetail(null)}
                onCancel={setCancelling}
              />
            )}
          </aside>
        </div>
      </Card>

      {detail && !hasPanel && (
        <BookingDetailDialog
          item={detail}
          booking={detailBooking.data ?? null}
          error={detailError}
          canBook={canCreate}
          canCancel={canCancel}
          onClose={() => setDetail(null)}
          onCancel={setCancelling}
        />
      )}

      {cancelling && (
        <CancelBookingDialog token={token} booking={cancelling} onClose={() => setCancelling(null)} onCancelled={handleCancelled} />
      )}

      {quickBook && (
        <QuickBookDialog
          token={token}
          window={quickBook}
          slotMinutes={building.slotMinutes}
          today={now.date}
          firstBookableMinute={firstBookableMinute}
          onClose={() => setQuickBook(null)}
          onPick={(room, attendees, picked) => {
            setForm({ room, window: picked, attendees })
            setQuickBook(null)
          }}
        />
      )}

      {form && (
        <BookingForm
          token={token}
          building={building}
          space={form.room.space}
          floorName={form.room.floorName}
          initialSlot={{
            date: form.window.date,
            start: fromMinutes(form.window.start),
            end: fromMinutes(form.window.end),
          }}
          initialAttendees={form.attendees}
          onClose={() => setForm(null)}
          onBooked={handleBooked}
        />
      )}

    </>
  )
}
