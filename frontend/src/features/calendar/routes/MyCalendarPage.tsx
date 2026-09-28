import { useEffect, useMemo, useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { useSearchParams } from 'react-router-dom'
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { cn } from '@/lib/utils'
import { Toast, useToast } from '../../../components/Toast'
import { TextSkeleton } from '../../../components/LoadingSkeletons'
import { useAsync } from '../../../hooks/useAsync'
import { useMediaQuery } from '../../../hooks/useMediaQuery'
import { dateOf, formatDate, fromMinutes, nextSlot, nowInZone, timeOf, toMinutes } from '../../../lib/time/buildingTime'
import type { IsoDate } from '../../../lib/time/buildingTime'
import { ApiError, getMyBookableBuilding } from '../../bookings/api/bookingsApi'
import type { BookableBuildingDto, BookingDto, SpaceAvailabilityDto } from '../../bookings/api/bookingsApi'
import { emitBookingsChanged, useBookingsChanged } from '../../bookings/bookingEvents'
import { BookingForm } from '../../bookings/components/BookingForm'
import { readLastDuration } from '../../bookings/preferences'
import { suggestWindow } from '../../bookings/suggestSlot'
import { daysBetween, gridMonthFor, isValidIsoDate, rangeLabel, shiftDate, startOfMonth, visibleRange } from '../calendarDates'
import { useMonthBookings, useMonthBookingsCache } from '../useMonthBookings'
import type { CalendarView } from '../calendarDates'
import { bookingMinutes, hourSpan } from '../dayLayout'
import { buildDurationLimits } from '../durationLimits'
import { BookingDetailDialog } from '../components/BookingDetailDialog'
import { CancelBookingDialog } from '../components/CancelBookingDialog'
import { MiniCalendar } from '../components/MiniCalendar'
import { MonthGrid } from '../components/MonthGrid'
import { QuickBookDialog } from '../components/QuickBookDialog'
import type { QuickBookWindow } from '../components/QuickBookDialog'
import { TimeGrid } from '../components/TimeGrid'

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
  const [now, setNow] = useState(() => nowInZone(timeZone))
  useEffect(() => {
    setNow(nowInZone(timeZone))
    const timer = setInterval(() => setNow(nowInZone(timeZone)), 30_000)
    return () => clearInterval(timer)
  }, [timeZone])
  return now
}

/**
 * My calendar: every booking I've made, on the building's clock, as a Day, Week or Month.
 * Click a booking to see it or cancel it; drag across empty time (or click it) to see
 * which rooms are free then and book one, without leaving the page.
 */
export function MyCalendarPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { status, data: building, error } = useAsync(() => getMyBookableBuilding(token), [token])

  return (
    <>
      <div className="top">
        <span className="pick">
          <span className="picklbl">
            {status === 'loading' ? <TextSkeleton label="Loading your building…" /> : building?.name}
          </span>
        </span>
      </div>

      <div className="content">
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

        {status === 'success' && building && <Calendar token={token} building={building} />}
      </div>
    </>
  )
}

function Calendar({ token, building }: { token: string; building: BookableBuildingDto }) {
  const { toast, showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const now = useZonedNow(building.timezone)
  const wide = useMediaQuery('(min-width: 860px)')

  // ?view=day|week|month&date=YYYY-MM-DD — a refresh or a shared link lands on the same
  // page. The view falls back to the one used last; phones only get the Day view.
  const requestedView = searchParams.get('view')
  const chosenView: CalendarView = VIEWS.includes(requestedView as CalendarView) ? (requestedView as CalendarView) : readView()
  const view: CalendarView = wide ? chosenView : 'day'
  const requestedDate = searchParams.get('date')
  const date: IsoDate = isValidIsoDate(requestedDate) ? requestedDate : now.date

  function go(next: { view?: CalendarView; date?: IsoDate }) {
    const v = next.view ?? chosenView
    if (next.view) rememberView(next.view)
    setSearchParams({ view: v, date: next.date ?? date }, { replace: true })
  }

  const [miniMonth, setMiniMonth] = useState(date)
  useEffect(() => setMiniMonth(date), [date])

  // One request per month grid, cached, with the months either side prefetched — the
  // week, day and mini calendar all read from it, so moving around is instant.
  const range = visibleRange(view, date)
  const { cache, version, refresh } = useMonthBookingsCache(token)
  const bookings = useMonthBookings(cache, gridMonthFor(view, date), version, true)
  const miniBookings = useMonthBookings(cache, startOfMonth(miniMonth), version)

  // Made here, made in Find a space, or cancelled: everything cached may be out of date.
  useBookingsChanged(refresh)

  const [detail, setDetail] = useState<BookingDto | null>(null)
  const [cancelling, setCancelling] = useState<BookingDto | null>(null)
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
      else if (wide && key === 'd') go({ view: 'day' })
      else if (wide && key === 'w') go({ view: 'week' })
      else if (wide && key === 'm') go({ view: 'month' })
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
  // Memoised so the time grid's day columns get the same objects between renders.
  const hours = useMemo(
    () => hourSpan(building, items.map((b) => bookingMinutes(b, dateOf(b.localStart)))),
    [building, items],
  )
  // Every room's max length, sorted once per building — the drag checks it on each move.
  const limits = useMemo(() => buildDurationLimits(building), [building])
  const defaultLength = Math.min(readLastDuration() ?? 60, limits.longest)
  const firstBookableMinute = nextSlot(now.minutes + building.minLeadMinutes, building.slotMinutes)

  function newBooking() {
    const w = suggestWindow(building)
    setQuickBook({ date: w.date, start: toMinutes(w.start), end: toMinutes(w.end) })
  }

  function handleBooked(created: BookingDto) {
    setForm(null)
    showToast(`Booked ${created.spaceName} — ${formatDate(dateOf(created.localStart))}, ${timeOf(created.localStart)}–${timeOf(created.localEnd)}`)
    // Jump to the new booking if it's off screen.
    const day = dateOf(created.localStart)
    if (day < range.from || day >= range.to) go({ date: day })
  }

  function handleCancelled(cancelled: BookingDto) {
    setCancelling(null)
    setDetail(null)
    emitBookingsChanged()
    showToast(`Cancelled — ${cancelled.spaceName} is free again for ${timeOf(cancelled.localStart)}–${timeOf(cancelled.localEnd)}`)
  }

  return (
    <>
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <Button onClick={newBooking}>
          <Plus /> New booking
        </Button>
        <Button variant="outline" onClick={() => go({ date: now.date })} title="Today (T)">
          Today
        </Button>
        <div className="flex">
          <Button variant="ghost" size="icon" aria-label="Previous" title="Previous (←)" onClick={() => go({ date: shiftDate(view, date, -1) })}>
            <ChevronLeft />
          </Button>
          <Button variant="ghost" size="icon" aria-label="Next" title="Next (→)" onClick={() => go({ date: shiftDate(view, date, 1) })}>
            <ChevronRight />
          </Button>
        </div>
        <h2 className="font-[family-name:var(--font-display)] text-xl font-semibold" aria-live="polite">
          {rangeLabel(view, date)}
        </h2>

        <span className="flex-1" />

        {wide && (
          <ToggleGroup
            type="single"
            variant="outline"
            size="sm"
            value={view}
            onValueChange={(v) => v && go({ view: v as CalendarView })}
            aria-label="View"
          >
            <ToggleGroupItem value="day" title="Day (D)">
              Day
            </ToggleGroupItem>
            <ToggleGroupItem value="week" title="Week (W)">
              Week
            </ToggleGroupItem>
            <ToggleGroupItem value="month" title="Month (M)">
              Month
            </ToggleGroupItem>
          </ToggleGroup>
        )}
        <span className="text-sm text-muted-foreground">
          {building.name} · {building.timezone}
        </span>
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[252px_minmax(0,1fr)]">
        <aside className="hidden flex-col gap-3 lg:flex">
          <MiniCalendar
            selected={date}
            month={miniMonth}
            today={now.date}
            bookedDays={[...new Set((miniBookings.data ?? []).map((b) => dateOf(b.localStart)))]}
            onSelect={(d) => go({ date: d })}
            onMonthChange={setMiniMonth}
          />
        </aside>

        <section
          className={cn('min-w-0 transition-opacity', bookings.isRefreshing && bookings.status === 'success' && 'opacity-70')}
          aria-busy={bookings.isRefreshing}
        >
          {bookings.status === 'error' && (
            <p role="alert" className="mb-3 rounded-md bg-slot-closed px-4 py-3 text-sm text-slot-closed-ink">
              {bookings.error instanceof ApiError ? bookings.error.message : "Couldn't load your bookings — please try again."}
            </p>
          )}

          {view === 'month' ? (
            <MonthGrid
              date={date}
              bookings={items}
              today={now.date}
              onOpenBooking={setDetail}
              onOpenDay={(d) => go({ view: 'day', date: d })}
            />
          ) : (
            <TimeGrid
              days={days}
              bookings={items}
              hours={hours}
              today={now.date}
              nowMinute={now.minutes}
              firstBookableMinute={firstBookableMinute}
              leadMinutes={building.minLeadMinutes}
              slotMinutes={building.slotMinutes}
              defaultLength={defaultLength}
              limits={limits}
              onOpenBooking={setDetail}
              onPickRange={setQuickBook}
              onOpenDay={view === 'week' ? (d) => go({ view: 'day', date: d }) : undefined}
            />
          )}

          {bookings.status === 'success' && items.length === 0 && (
            <p className="mt-3 text-center text-sm text-muted-foreground">
              Nothing booked {view === 'day' ? 'this day' : view === 'week' ? 'this week' : 'this month'}.{' '}
              <button type="button" className="font-medium text-foreground underline underline-offset-4" onClick={newBooking}>
                Book a room
              </button>
            </p>
          )}
        </section>
      </div>

      {detail && <BookingDetailDialog booking={detail} onClose={() => setDetail(null)} onCancel={setCancelling} />}

      {cancelling && (
        <CancelBookingDialog token={token} booking={cancelling} onClose={() => setCancelling(null)} onCancelled={handleCancelled} />
      )}

      {quickBook && (
        <QuickBookDialog
          token={token}
          window={quickBook}
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

      <Toast toast={toast} />
    </>
  )
}
