import { memo, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Repeat, Users } from 'lucide-react'
import type { PointerEvent } from 'react'
import { cn } from '@/lib/utils'
import { formatDate, fromMinutes, timeOf } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { OperatingWindowDto } from '@/features/space-management/api/spaceManagementApi'
import { dayOfMonth, shortWeekday, weekdayName } from '@/features/calendar/calendarDates'
import { formatClock, formatClockRange } from '@/lib/time/format'
import { formatDuration } from '@/features/bookings/format'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { DAY_MINUTES, HOUR_PX, itemMinutes, itemsByDay, layoutDay, openWindow } from '@/features/calendar/dayLayout'
import { dragHint } from '@/features/calendar/durationLimits'
import type { DurationLimits } from '@/features/calendar/durationLimits'

// Movement below this many pixels is a click, not a drag.
const DRAG_THRESHOLD_PX = 4

const NO_ITEMS: CalendarItem[] = []

// Diagonal hatching for the hours the building is shut — the same idea as the closed
// stretches on Find a space's day bars.
const CLOSED_HATCH =
  'repeating-linear-gradient(135deg, color-mix(in srgb, var(--border-subtle) 70%, transparent) 0 4px, transparent 4px 10px)'

// A booking that has started (or is over) is hatched in its own tint — still readable,
// clearly not ahead of you any more.
const STARTED_HATCH =
  'repeating-linear-gradient(135deg, transparent 0 5px, color-mix(in srgb, var(--focus-ring) 14%, transparent) 5px 6px)'

type OpenWindow = { from: number; to: number } | null

/** A minute's y on the grid — midnight is the top, so the day's minutes are the axis. */
const top = (minute: number) => (minute / 60) * HOUR_PX

export interface PickedRange {
  date: IsoDate
  start: number
  end: number
}

interface TimeGridProps {
  days: IsoDate[]
  items: CalendarItem[]
  /** The building's opening days (0 = Sunday … 6) and hours; everything else is shaded as closed. */
  openDays: number[]
  openHours: OperatingWindowDto
  today: IsoDate
  /** The building's current minute of the day. */
  nowMinute: number
  /** The earliest minute on `today` a new booking may start (now + notice). Past days can't be picked at all. */
  firstBookableMinute: number
  /** The last date that can be booked (today + the building's booking window); later days can't be picked. */
  lastBookableDate: IsoDate
  /** The building's minimum notice, to explain the band between "now" and the first bookable minute. */
  leadMinutes: number
  slotMinutes: number
  /** A plain click picks this many minutes from where it lands. */
  defaultLength: number
  /** Every room's max length — a drag stops at the longest and says how many rooms fit on the way. */
  limits: DurationLimits
  onOpenItem: (item: CalendarItem) => void
  onPickRange: (range: PickedRange) => void
  /** Week view: clicking a day's heading opens that day. */
  onOpenDay?: (date: IsoDate) => void
  /** Nothing can be booked (the building was removed): empty time doesn't respond. */
  readOnly?: boolean
}

/**
 * The Day and Week views: a column per day on one shared 24-hour axis. Items are blocks
 * (overlapping ones side by side); the past and the building's closed hours are shaded;
 * today has a live "now" line. Drag down across empty time to pick a window — or click
 * for the usual length — and the page offers the rooms that are free for it.
 */
export function TimeGrid({
  days,
  items,
  openDays,
  openHours,
  today,
  nowMinute,
  firstBookableMinute,
  lastBookableDate,
  leadMinutes,
  slotMinutes,
  defaultLength,
  limits,
  onOpenItem,
  onPickRange,
  onOpenDay,
  readOnly = false,
}: TimeGridProps) {
  const { t } = useTranslation()
  const scrollRef = useRef<HTMLDivElement>(null)
  // The body scrolls (native scrollbar); the day headings don't, so without this the
  // scrollbar's width would push the body's columns out of line with the headings above
  // them. Matched via ResizeObserver, since it comes and goes with the viewport's height.
  // The scrollbar sits at the inline end — the right in English, the left in Arabic — so
  // the headings are padded on that side, not always the right.
  const [scrollbarWidth, setScrollbarWidth] = useState(0)
  const height = 24 * HOUR_PX
  const showsToday = days.includes(today)
  // "Closed" is written once, in the first column that isn't past (the hatching marks
  // closed hours in every column) — seven identical labels in a row are just noise.
  const closedLabelDay = days.find((d) => d >= today) ?? days[0]

  const daysKey = days.join()
  // Grouped once per data change, so a column's list keeps its identity between renders
  // and the memoised columns below skip re-rendering. Same for each day's open window.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const byDay = useMemo(() => itemsByDay(items, days), [items, daysKey])
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const openByDay = useMemo(() => new Map(days.map((d) => [d, openWindow(d, openDays, openHours)])), [daysKey, openDays, openHours])

  // The whole day is drawn, but open scrolled to what matters: "now" (an hour of context
  // above it) when today is on screen, otherwise the first item — or the building's
  // opening hour, not the top of an empty night.
  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    const firstItem = items
      .map((b) => itemMinutes(b, days.find((d) => b.localStart.startsWith(d)) ?? days[0]).start)
      .sort((a, b) => a - b)[0]
    const opens = days.map((d) => openByDay.get(d)?.from).find((m) => m !== undefined) ?? 8 * 60
    const target = showsToday ? nowMinute - 60 : (firstItem ?? opens) - 30
    el.scrollTop = Math.max(0, top(target))
    // Only when the days change — not on every refetch or clock tick.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [daysKey])

  const hourMarks = Array.from({ length: 24 }, (_, h) => h)

  useEffect(() => {
    const el = scrollRef.current
    // Doesn't exist in the test environment — there's no real scrollbar to measure there.
    if (!el || typeof ResizeObserver !== 'function') return
    const measure = () => setScrollbarWidth(el.offsetWidth - el.clientWidth)
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  return (
    <div className="flex min-h-0 flex-col">
      {/* Day headings */}
      <div
        className="grid border-b"
        style={{ gridTemplateColumns: `64px repeat(${days.length}, minmax(0, 1fr))`, paddingInlineEnd: scrollbarWidth }}
      >
        <span aria-hidden="true" />
        {days.map((d) => {
          const isToday = d === today
          const label = (
            <>
              <span className="text-xs text-muted-foreground">{shortWeekday(d)}</span>
              <span
                className={cn(
                  'grid size-6 place-items-center rounded-full text-xs font-semibold',
                  isToday && 'bg-primary text-primary-foreground',
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
              className="flex items-center justify-center gap-1.5 border-s bg-transparent py-2.5 hover:bg-muted"
              aria-label={t('Calendar:OpenDay', { day: `${shortWeekday(d)} ${dayOfMonth(d)}` })}
              aria-current={isToday ? 'date' : undefined}
              onClick={() => onOpenDay(d)}
            >
              {label}
            </button>
          ) : (
            <div key={d} className="flex items-center justify-center gap-1.5 border-s py-2.5" aria-current={isToday ? 'date' : undefined}>
              {label}
            </div>
          )
        })}
      </div>

      {/* Hours × days */}
      <div ref={scrollRef} className="max-h-[calc(100vh-240px)] min-h-[360px] overflow-y-auto">
        <div className="relative grid" style={{ gridTemplateColumns: `64px repeat(${days.length}, minmax(0, 1fr))`, height }}>
          <div className="relative" aria-hidden="true">
            {hourMarks.map((h) => (
              <span
                key={h}
                className="absolute inset-e-2 -translate-y-1/2 text-[11px] text-muted-foreground"
                style={{ top: top(h * 60) }}
              >
                {/* Midnight has no label; an hour the "now" marker sits on top of steps aside. */}
                {h === 0 || (showsToday && Math.abs(h * 60 - nowMinute) < 20) ? '' : formatClock(fromMinutes(h * 60))}
              </span>
            ))}
            {showsToday && (
              <span
                className="absolute inset-e-0 z-30 flex -translate-y-1/2 items-center gap-1 bg-card ps-1 text-[11px] font-bold text-brand"
                style={{ top: top(nowMinute) }}
              >
                {formatClock(fromMinutes(nowMinute))}
                <span className="size-1.5 rounded-full bg-brand" />
              </span>
            )}
          </div>

          {/* "Now", once across every day on screen — the day columns only shade what's past. */}
          {showsToday && (
            <div
              className="pointer-events-none absolute inset-e-0 inset-s-16 z-30 border-t-[1.5px] border-brand"
              style={{ top: top(nowMinute) }}
              aria-hidden="true"
            />
          )}

          {days.map((d) => (
            <DayColumn
              key={d}
              date={d}
              items={byDay.get(d) ?? NO_ITEMS}
              open={openByDay.get(d) ?? null}
              past={d < today}
              isToday={d === today}
              labelClosed={d === closedLabelDay}
              // Only today's column cares what time it is — the others get a constant, so the
              // 30-second clock tick re-renders one column, not the whole week.
              nowMinute={d === today ? nowMinute : 0}
              firstBookableMinute={d === today ? firstBookableMinute : d < today || d > lastBookableDate ? Infinity : 0}
              bookableUntil={d > lastBookableDate ? lastBookableDate : null}
              readOnly={readOnly}
              leadMinutes={leadMinutes}
              slotMinutes={slotMinutes}
              defaultLength={defaultLength}
              limits={limits}
              onOpenItem={onOpenItem}
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
  items: CalendarItem[]
  /** When the building is open on this day; null when it's shut all day. */
  open: OpenWindow
  past: boolean
  /** Set on a day past the booking window: the last date that can be booked. */
  bookableUntil: IsoDate | null
  isToday: boolean
  /** Writes "Closed" on the closed hours; only one column on screen does. */
  labelClosed: boolean
  nowMinute: number
  firstBookableMinute: number
  leadMinutes: number
  slotMinutes: number
  defaultLength: number
  limits: DurationLimits
  onOpenItem: (item: CalendarItem) => void
  onPickRange: (range: PickedRange) => void
  readOnly: boolean
}

const DayColumn = memo(function DayColumn({
  date,
  items,
  open,
  past,
  bookableUntil,
  isToday,
  labelClosed,
  nowMinute,
  firstBookableMinute,
  leadMinutes,
  slotMinutes,
  defaultLength,
  limits,
  onOpenItem,
  onPickRange,
  readOnly,
}: DayColumnProps) {
  const { t } = useTranslation()
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

  // A closed day is "opens at the end of the day": nothing to book, and both shaded bands
  // below fall out of the same two numbers.
  const openFrom = open?.from ?? DAY_MINUTES
  const openTo = open?.to ?? DAY_MINUTES
  const snap = (m: number) => Math.floor(m / slotMinutes) * slotMinutes

  function minuteAt(clientY: number): number {
    const rect = ref.current!.getBoundingClientRect()
    return ((clientY - rect.top) / HOUR_PX) * 60
  }

  // Keeps a picked range inside the building's hours, after "now + notice" and no longer
  // than the longest any room allows. `anchor` says which end the pointer holds still, so
  // the length cap trims the end being dragged.
  function clamp(start: number, end: number, anchor: 'start' | 'end' = 'start') {
    let s = Math.max(start, openFrom, Math.ceil(firstBookableMinute / slotMinutes) * slotMinutes)
    let e = Math.min(Math.max(end, s + slotMinutes), openTo)
    if (e - s > limits.longest) {
      if (anchor === 'start') e = s + limits.longest
      else s = e - limits.longest
    }
    return { start: s, end: e }
  }

  // A slot that's entirely in the past, inside the notice period, on a past day, or
  // outside the building's hours can't be picked.
  const isBlocked = (minute: number) =>
    minute + slotMinutes <= firstBookableMinute || minute < openFrom || minute + slotMinutes > openTo
  const earliest = Math.ceil(firstBookableMinute / slotMinutes) * slotMinutes

  function whyBlocked(minute: number): string {
    if (past) return t('Calendar:DayPassed')
    if (bookableUntil) return t('Dixels:Bookings:BeyondHorizon:Short', { lastDate: formatDate(bookableUntil) })
    if (!open) return t('Calendar:ClosedOnWeekday', { weekday: weekdayName(date) })
    if (minute < nowMinute) return t('Calendar:TimePassed')
    if (minute < openFrom) return t('Calendar:OpensAt', { time: fromMinutes(openFrom) })
    if (minute + slotMinutes > openTo) return t('Calendar:ClosesAt', { time: fromMinutes(openTo) })
    return t('Calendar:TooSoon', { notice: formatDuration(leadMinutes), time: fromMinutes(earliest) })
  }

  function handlePointerDown(e: PointerEvent<HTMLDivElement>) {
    if (readOnly || e.button !== 0 || e.target !== e.currentTarget) return
    const minute = snap(minuteAt(e.clientY))
    if (minute >= DAY_MINUTES) return
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
      if (readOnly) return
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
  const placed = layoutDay(items.map((b) => ({ item: b, ...itemMinutes(b, date) })))
  const pastUntil = past ? DAY_MINUTES : isToday ? Math.min(Math.max(nowMinute, 0), DAY_MINUTES) : 0
  // Today, between "now" and the first bookable slot: not past, but too soon to book.
  const noticeUntil = isToday ? Math.min(Math.max(earliest, pastUntil), DAY_MINUTES) : pastUntil

  return (
    <div
      ref={ref}
      className={cn('relative touch-pan-y border-s select-none', readOnly ? 'cursor-default' : hover === 'blocked' ? 'cursor-not-allowed' : 'cursor-pointer')}
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
      {/* Closed before opening (the whole day when the building is shut), and after closing. */}
      {openFrom > 0 && (
        <div
          className="pointer-events-none absolute inset-x-0 top-0 flex items-end justify-end bg-muted/40 px-1.5 pb-0.5"
          style={{ height: top(openFrom), backgroundImage: CLOSED_HATCH }}
          aria-hidden="true"
        >
          {openFrom >= 30 && (labelClosed || !open) && (
            <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">{open ? t('Calendar:Closed') : t('Calendar:ClosedAllDay')}</span>
          )}
        </div>
      )}
      {openTo < DAY_MINUTES && (
        <div
          className="pointer-events-none absolute inset-x-0 flex items-start justify-end bg-muted/40 px-1.5 pt-0.5"
          style={{ top: top(openTo), height: top(DAY_MINUTES) - top(openTo), backgroundImage: CLOSED_HATCH }}
          aria-hidden="true"
        >
          {DAY_MINUTES - openTo >= 30 && labelClosed && (
            <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">{t('Calendar:Closed')}</span>
          )}
        </div>
      )}

      {pastUntil > 0 && (
        <div
          className="pointer-events-none absolute inset-x-0 top-0 flex items-end justify-end bg-muted/70 px-1.5 pb-0.5"
          style={{ height: top(pastUntil) }}
          aria-hidden="true"
        >
          {pastUntil >= 30 && (
            <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">{t('Calendar:Past')}</span>
          )}
        </div>
      )}

      {bookableUntil && (
        <div
          className="pointer-events-none absolute inset-0 flex items-start justify-end bg-muted/40 px-1.5 pt-0.5"
          style={{ backgroundImage: CLOSED_HATCH }}
          aria-hidden="true"
        >
          <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">{t('Unavailable:BeyondHorizon')}</span>
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
          {noticeUntil - pastUntil >= 10 && t('Calendar:Notice', { notice: formatDuration(leadMinutes) })}
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
        const height = Math.max(top(end) - top(start) - 2, 18)
        // How many 16px lines fit inside the padding and border. Too short for two, the time
        // and the space share one line ("17:30 · Desk 32") instead of the time being cut off.
        const lines = Math.floor((height - 10) / 16)
        const started = past || (isToday && start <= nowMinute)
        return (
          <button
            key={b.id}
            type="button"
            className={cn(
              'absolute z-10 flex flex-col overflow-hidden rounded-lg border border-brand/30 bg-slot-open px-2 py-1 text-start text-xs shadow-xs',
              'transition-colors hover:border-brand/70 hover:bg-[color-mix(in_srgb,var(--focus-ring)_22%,var(--surface-raised))]',
              'focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
              // Cancelled by an admin: still shown so the person knows why it went, but clearly not theirs any more.
              // Someone else's booking I'm invited to: an outline on the plain background.
              b.invited && !b.cancelled && 'border-[1.5px] border-dashed border-brand bg-background shadow-none',
              b.cancelled && 'border-dashed border-muted-foreground/50 bg-muted text-muted-foreground line-through opacity-80 shadow-none hover:bg-muted',
            )}
            style={{
              top: top(start) + 1,
              height,
              // From the start side, so side-by-side bookings read in the language's direction.
              insetInlineStart: `calc(${(lane / lanes) * 100}% + 2px)`,
              width: `calc(${100 / lanes}% - 4px)`,
              ...(started && !b.cancelled ? { backgroundImage: STARTED_HATCH } : {}),
            }}
            aria-label={t(b.cancelled ? 'Calendar:CancelledItem' : b.invited ? 'Calendar:InvitedItem' : 'Calendar:Item', {
              title: b.title,
              start: timeOf(b.localStart),
              end: timeOf(b.localEnd),
              location: b.location,
            })}
            onPointerDown={(e) => e.stopPropagation()}
            onClick={() => onOpenItem(b)}
          >
            <span className={cn('flex min-w-0 items-center gap-1 font-semibold', !b.cancelled && 'text-brand')}>
              {b.invited && <Users className="size-3 flex-none" aria-hidden="true" />}
              {b.repeats && <Repeat className="size-3 flex-none" aria-label={t('Calendar:Repeats')} />}
              <span className="truncate">
                {lines < 2 ? `${formatClock(timeOf(b.localStart))} · ${b.location}` : b.location}
              </span>
            </span>
            {lines >= 2 && (
              <span className={cn('truncate text-[11px]', b.cancelled ? 'text-muted-foreground' : 'text-brand/80')}>
                {formatClockRange(timeOf(b.localStart), timeOf(b.localEnd))}
              </span>
            )}
            {lines >= 3 && b.note && <span className="truncate text-[11px] text-muted-foreground">{b.note}</span>}
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
              {formatClockRange(fromMinutes(draft.start), fromMinutes(draft.end))}
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
