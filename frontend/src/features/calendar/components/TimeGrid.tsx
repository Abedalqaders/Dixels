import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Repeat } from 'lucide-react'
import type { PointerEvent } from 'react'
import { cn } from '@/lib/utils'
import { fromMinutes, timeOf } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { dayOfMonth, shortWeekday } from '@/features/calendar/calendarDates'
import { bookingMinutes, bookingsByDay, HOUR_PX, layoutDay } from '@/features/calendar/dayLayout'
import { dragHint } from '@/features/calendar/durationLimits'
import type { DurationLimits } from '@/features/calendar/durationLimits'

// Movement below this many pixels is a click, not a drag.
const DRAG_THRESHOLD_PX = 4

const NO_BOOKINGS: BookingDto[] = []

export interface PickedRange {
  date: IsoDate
  start: number
  end: number
}

interface TimeGridProps {
  days: IsoDate[]
  bookings: BookingDto[]
  hours: { from: number; to: number }
  today: IsoDate
  /** The building's current minute of the day. */
  nowMinute: number
  /** The earliest minute on `today` a new booking may start (now + notice). Past days can't be picked at all. */
  firstBookableMinute: number
  /** The building's minimum notice, to explain the band between "now" and the first bookable minute. */
  leadMinutes: number
  slotMinutes: number
  /** A plain click picks this many minutes from where it lands. */
  defaultLength: number
  /** Every room's max length — a drag stops at the longest and says how many rooms fit on the way. */
  limits: DurationLimits
  onOpenBooking: (booking: BookingDto) => void
  onPickRange: (range: PickedRange) => void
  /** Week view: clicking a day's heading opens that day. */
  onOpenDay?: (date: IsoDate) => void
}

/**
 * The Day and Week views: a column per day on one shared hour axis. Bookings are blocks
 * (overlapping ones side by side); the past is shaded; today has a live "now" line. Drag
 * down across empty time to pick a window — or click for the usual length — and the page
 * offers the rooms that are free for it.
 */
export function TimeGrid({
  days,
  bookings,
  hours,
  today,
  nowMinute,
  firstBookableMinute,
  leadMinutes,
  slotMinutes,
  defaultLength,
  limits,
  onOpenBooking,
  onPickRange,
  onOpenDay,
}: TimeGridProps) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const top = useCallback((minute: number) => ((minute - hours.from * 60) / 60) * HOUR_PX, [hours.from])
  const height = (hours.to - hours.from) * HOUR_PX
  const showsToday = days.includes(today)

  // Open scrolled to "now" (an hour of context above it) when today is on screen,
  // otherwise to the first booking — not to the top of a long empty morning.
  const daysKey = days.join()
  // Grouped once per data change, so a column's list keeps its identity between renders
  // and the memoised columns below skip re-rendering.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const byDay = useMemo(() => bookingsByDay(bookings, days), [bookings, daysKey])
  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    const firstBooking = bookings
      .map((b) => bookingMinutes(b, days.find((d) => b.localStart.startsWith(d)) ?? days[0]).start)
      .sort((a, b) => a - b)[0]
    const target = showsToday ? nowMinute - 60 : (firstBooking ?? hours.from * 60) - 30
    el.scrollTop = Math.max(0, top(target))
    // Only when the days change — not on every refetch or clock tick.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [daysKey])

  const hourMarks: number[] = []
  for (let h = hours.from; h < hours.to; h++) hourMarks.push(h)

  return (
    <div className="flex min-h-0 flex-col overflow-hidden rounded-xl border bg-card">
      {/* Day headings */}
      <div
        className="grid border-b"
        style={{ gridTemplateColumns: `56px repeat(${days.length}, minmax(0, 1fr))` }}
      >
        <span aria-hidden="true" />
        {days.map((d) => {
          const isToday = d === today
          const label = (
            <>
              <span className="text-xs tracking-wide text-muted-foreground uppercase">{shortWeekday(d)}</span>
              <span
                className={cn(
                  'grid size-8 place-items-center rounded-full text-base font-semibold',
                  isToday && 'bg-brand text-primary-foreground',
                )}
              >
                {dayOfMonth(d)}
              </span>
            </>
          )
          return onOpenDay ? (
            <button
              key={d}
              type="button"
              className="flex flex-col items-center gap-0.5 border-l py-2 hover:bg-muted"
              aria-label={`Open ${shortWeekday(d)} ${dayOfMonth(d)}`}
              aria-current={isToday ? 'date' : undefined}
              onClick={() => onOpenDay(d)}
            >
              {label}
            </button>
          ) : (
            <div key={d} className="flex flex-col items-center gap-0.5 border-l py-2" aria-current={isToday ? 'date' : undefined}>
              {label}
            </div>
          )
        })}
      </div>

      {/* Hours × days */}
      <div ref={scrollRef} className="max-h-[calc(100vh-240px)] min-h-[360px] overflow-y-auto">
        <div className="grid" style={{ gridTemplateColumns: `56px repeat(${days.length}, minmax(0, 1fr))`, height }}>
          <div className="relative" aria-hidden="true">
            {hourMarks.map((h) => (
              <span
                key={h}
                className="absolute right-2 -translate-y-1/2 font-mono text-[11px] text-muted-foreground"
                style={{ top: top(h * 60) }}
              >
                {h === hours.from ? '' : fromMinutes(h * 60)}
              </span>
            ))}
          </div>

          {days.map((d) => (
            <DayColumn
              key={d}
              date={d}
              bookings={byDay.get(d) ?? NO_BOOKINGS}
              hours={hours}
              past={d < today}
              isToday={d === today}
              // Only today's column cares what time it is — the others get a constant, so the
              // 30-second clock tick re-renders one column, not the whole week.
              nowMinute={d === today ? nowMinute : 0}
              firstBookableMinute={d === today ? firstBookableMinute : d < today ? Infinity : 0}
              leadMinutes={leadMinutes}
              slotMinutes={slotMinutes}
              defaultLength={defaultLength}
              limits={limits}
              top={top}
              onOpenBooking={onOpenBooking}
              onPickRange={onPickRange}
            />
          ))}
        </div>
      </div>
    </div>
  )
}

interface DayColumnProps {
  date: IsoDate
  bookings: BookingDto[]
  hours: { from: number; to: number }
  past: boolean
  isToday: boolean
  nowMinute: number
  firstBookableMinute: number
  leadMinutes: number
  slotMinutes: number
  defaultLength: number
  limits: DurationLimits
  top: (minute: number) => number
  onOpenBooking: (booking: BookingDto) => void
  onPickRange: (range: PickedRange) => void
}

const DayColumn = memo(function DayColumn({
  date,
  bookings,
  hours,
  past,
  isToday,
  nowMinute,
  firstBookableMinute,
  leadMinutes,
  slotMinutes,
  defaultLength,
  limits,
  top,
  onOpenBooking,
  onPickRange,
}: DayColumnProps) {
  const ref = useRef<HTMLDivElement>(null)
  const drag = useRef<{ anchor: number; startY: number; moved: boolean } | null>(null)
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null)
  // Where a click would book, shown under the pointer before pressing, or 'blocked' when
  // the pointer is over time that can't be booked (so the cursor says so too).
  const [hover, setHover] = useState<{ start: number; end: number } | 'blocked' | null>(null)
  // Why a click on shaded time did nothing, shown briefly where it was clicked.
  const [notice, setNotice] = useState<{ minute: number; text: string } | null>(null)

  useEffect(() => {
    if (!notice) return
    const timer = setTimeout(() => setNotice(null), 3000)
    return () => clearTimeout(timer)
  }, [notice])

  const dayStart = hours.from * 60
  const dayEnd = hours.to * 60
  const snap = (m: number) => Math.floor(m / slotMinutes) * slotMinutes

  function minuteAt(clientY: number): number {
    const rect = ref.current!.getBoundingClientRect()
    return dayStart + ((clientY - rect.top) / HOUR_PX) * 60
  }

  // Keeps a picked range on the grid, inside the day's hours, after "now + notice" and no
  // longer than the longest any room allows. `anchor` says which end the pointer holds
  // still, so the length cap trims the end being dragged.
  function clamp(start: number, end: number, anchor: 'start' | 'end' = 'start') {
    let s = Math.max(start, dayStart, Math.ceil(firstBookableMinute / slotMinutes) * slotMinutes)
    let e = Math.min(Math.max(end, s + slotMinutes), dayEnd)
    if (e - s > limits.longest) {
      if (anchor === 'start') e = s + limits.longest
      else s = e - limits.longest
    }
    return { start: s, end: e }
  }

  // A slot that's entirely in the past, inside the notice period, or on a past day can't be picked.
  const isBlocked = (minute: number) => minute + slotMinutes <= firstBookableMinute || minute >= dayEnd
  const earliest = Math.ceil(firstBookableMinute / slotMinutes) * slotMinutes

  function whyBlocked(minute: number): string {
    if (past) return 'This day has passed.'
    if (minute < nowMinute) return 'That time has passed.'
    return `Too soon: bookings need ${leadMinutes} min notice. The earliest you can start today is ${fromMinutes(earliest)}.`
  }

  function handlePointerDown(e: PointerEvent<HTMLDivElement>) {
    if (e.button !== 0 || e.target !== e.currentTarget) return
    const minute = snap(minuteAt(e.clientY))
    if (minute >= dayEnd) return
    if (isBlocked(minute)) {
      setNotice({ minute, text: whyBlocked(minute) })
      return
    }

    setNotice(null)
    setHover(null)
    e.currentTarget.setPointerCapture(e.pointerId)
    drag.current = { anchor: minute, startY: e.clientY, moved: false }
    setDraft(clamp(minute, minute + defaultLength))
  }

  function handlePointerMove(e: PointerEvent<HTMLDivElement>) {
    const d = drag.current
    if (!d) {
      // Not dragging: preview what a click here would pick. Only re-renders when the
      // pointer crosses into another slot, not on every pixel.
      if (e.pointerType === 'touch' || e.target !== e.currentTarget) {
        setHover(null)
        return
      }
      const minute = snap(minuteAt(e.clientY))
      const next = isBlocked(minute) ? 'blocked' : clamp(minute, minute + defaultLength)
      setHover((prev) => (sameHover(prev, next) ? prev : next))
      return
    }
    if (Math.abs(e.clientY - d.startY) > DRAG_THRESHOLD_PX) d.moved = true
    if (!d.moved) return
    const pointer = minuteAt(e.clientY)
    setDraft(
      pointer >= d.anchor
        ? clamp(d.anchor, Math.ceil(pointer / slotMinutes) * slotMinutes)
        : clamp(snap(pointer), d.anchor + slotMinutes, 'end'),
    )
  }

  function handlePointerUp() {
    const picked = draft
    drag.current = null
    setDraft(null)
    if (picked && picked.end > picked.start) onPickRange({ date, ...picked })
  }

  const hint = draft ? dragHint(draft.end - draft.start, limits) : null
  const placed = layoutDay(bookings.map((b) => ({ item: b, ...bookingMinutes(b, date) })))
  const pastUntil = past ? dayEnd : isToday ? Math.min(Math.max(nowMinute, dayStart), dayEnd) : dayStart
  // Today, between "now" and the first bookable slot: not past, but too soon to book.
  const noticeUntil = isToday ? Math.min(Math.max(earliest, pastUntil), dayEnd) : pastUntil

  return (
    <div
      ref={ref}
      className={cn('relative touch-pan-y border-l select-none', hover === 'blocked' ? 'cursor-not-allowed' : 'cursor-pointer')}
      style={{
        // Hour lines, with a fainter half-hour line between them.
        backgroundImage: `repeating-linear-gradient(to bottom, var(--border-subtle) 0 1px, transparent 1px ${HOUR_PX / 2}px, color-mix(in srgb, var(--border-subtle) 45%, transparent) ${HOUR_PX / 2}px ${HOUR_PX / 2 + 1}px, transparent ${HOUR_PX / 2 + 1}px ${HOUR_PX}px)`,
      }}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerLeave={() => setHover(null)}
      onPointerCancel={() => {
        drag.current = null
        setDraft(null)
      }}
    >
      {pastUntil > dayStart && (
        <div
          className="pointer-events-none absolute inset-x-0 top-0 flex items-end justify-end bg-muted/70 px-1.5 pb-0.5"
          style={{ height: top(pastUntil) }}
          aria-hidden="true"
        >
          {pastUntil - dayStart >= 30 && (
            <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">Past</span>
          )}
        </div>
      )}

      {noticeUntil > pastUntil && (
        <div
          className="pointer-events-none absolute inset-x-0 flex items-center justify-end overflow-hidden px-1.5 text-[10px] font-medium text-[var(--state-expired-ink)]"
          style={{
            top: top(pastUntil),
            height: top(noticeUntil) - top(pastUntil),
            backgroundImage:
              'repeating-linear-gradient(135deg, var(--state-expired-soft) 0 5px, color-mix(in srgb, var(--state-expired-soft) 40%, transparent) 5px 10px)',
          }}
          aria-hidden="true"
        >
          {noticeUntil - pastUntil >= 10 && `${leadMinutes} min notice`}
        </div>
      )}

      {hover && hover !== 'blocked' && !draft && (
        <div
          className="pointer-events-none absolute inset-x-1 z-10 flex items-start justify-center rounded-md border-2 border-dashed border-brand bg-[color-mix(in_srgb,var(--focus-ring)_14%,transparent)] pt-0.5 font-mono text-[11px] font-semibold text-brand"
          style={{ top: top(hover.start), height: top(hover.end) - top(hover.start) }}
          aria-hidden="true"
        >
          + {fromMinutes(hover.start)}
        </div>
      )}

      {placed.map(({ item: b, start, end, lane, lanes }) => {
        const tall = end - start >= 45
        const done = past || (isToday && end <= nowMinute)
        return (
          <button
            key={b.id}
            type="button"
            className={cn(
              'absolute z-10 flex flex-col overflow-hidden rounded-md border-l-[3px] border-brand bg-slot-open px-1.5 py-0.5 text-left text-xs shadow-xs',
              'transition-colors hover:bg-[color-mix(in_srgb,var(--focus-ring)_26%,var(--surface-raised))]',
              'focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
              done && 'opacity-60',
            )}
            style={{
              top: top(start) + 1,
              height: Math.max(top(end) - top(start) - 2, 18),
              left: `calc(${(lane / lanes) * 100}% + 2px)`,
              width: `calc(${100 / lanes}% - 4px)`,
            }}
            aria-label={`${b.title}, ${timeOf(b.localStart)}–${timeOf(b.localEnd)}, ${b.spaceName}`}
            onPointerDown={(e) => e.stopPropagation()}
            onClick={() => onOpenBooking(b)}
          >
            <span className="flex min-w-0 items-center gap-1 font-semibold">
              {b.seriesId && <Repeat className="size-3 flex-none text-brand" aria-label="Repeats" />}
              <span className="truncate">{b.title}</span>
            </span>
            {tall ? (
              <>
                <span className="truncate font-mono text-[11px] text-muted-foreground">
                  {timeOf(b.localStart)}–{timeOf(b.localEnd)}
                </span>
                <span className="truncate text-[11px] text-muted-foreground">{b.spaceName}</span>
              </>
            ) : null}
          </button>
        )
      })}

      {draft && hint && (
        <>
          <div
            className={cn(
              'pointer-events-none absolute inset-x-1 z-20 flex flex-col items-center rounded-md border-2 pt-0.5 text-[11px] font-medium',
              hint.capped
                ? 'border-[var(--state-expired-ink)] bg-[var(--state-expired-soft)] text-[var(--state-expired-ink)]'
                : 'border-brand bg-[color-mix(in_srgb,var(--focus-ring)_32%,var(--surface-raised))] text-foreground shadow-md',
            )}
            style={{ top: top(draft.start), height: top(draft.end) - top(draft.start) }}
            aria-hidden="true"
          >
            <span className="font-mono">
              {fromMinutes(draft.start)}–{fromMinutes(draft.end)}
            </span>
            {hint.suffix && <span className="px-1 text-center leading-tight">{hint.suffix}</span>}
          </div>
          <span className="sr-only" aria-live="polite">
            {hint.announcement}
          </span>
        </>
      )}

      {notice && (
        <div
          role="status"
          className="pointer-events-none absolute inset-x-1 z-40 rounded-md bg-foreground px-2 py-1.5 text-[11px] leading-snug text-background shadow-lg"
          style={{ top: Math.max(top(notice.minute) - 4, 0) }}
        >
          {notice.text}
        </div>
      )}

      {isToday && nowMinute >= dayStart && nowMinute <= dayEnd && (
        <div className="pointer-events-none absolute inset-x-0 z-30 h-0.5 bg-destructive" style={{ top: top(nowMinute) }} aria-hidden="true">
          <span className="absolute -top-[5px] -left-[5px] size-3 rounded-full bg-destructive" />
        </div>
      )}
    </div>
  )
})

type Hover = { start: number; end: number } | 'blocked' | null

// Equal hovers keep the previous state object, so moving within one slot doesn't re-render.
function sameHover(a: Hover, b: Hover): boolean {
  if (a === b) return true
  if (!a || !b || a === 'blocked' || b === 'blocked') return false
  return a.start === b.start && a.end === b.end
}
