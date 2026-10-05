import { useRef, useState } from 'react'
import type { KeyboardEvent, PointerEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { fromMinutes } from '@/lib/time/buildingTime'
import type { DayRangeDto } from '@/features/bookings/api/bookingsApi'
import type { DayAxis } from '@/features/bookings/dayAxis'
import { clickRange, dragRange, freeStretchAt } from '@/features/bookings/dragRange'
import type { FreeTimeRules, MinuteRange } from '@/features/bookings/dragRange'

export interface DayBarPick {
  rules: FreeTimeRules
  /** A plain click picks this length (the employee's usual one) from where they clicked. */
  defaultLength: number
  roomName: string
  onPick: (range: MinuteRange) => void
}

interface DayBarProps {
  axis: DayAxis
  open: DayRangeDto[]
  closed: DayRangeDto[]
  busy: DayRangeDto[]
  /** The searched window, outlined on the bar. */
  selection: { startMinute: number; endMinute: number }
  /** A short spoken summary of the room's day. */
  label: string
  /** When set, the bar is a picker: drag across free time (or click, or use the keyboard) to book it. */
  pick?: DayBarPick
}

// Outside opening hours: a faint hatch in the theme's neutral tones.
const OUTSIDE_HOURS = {
  backgroundImage:
    'repeating-linear-gradient(135deg, var(--accent-soft) 0 4px, var(--border-subtle) 4px 6px)',
}

// Movement below this many pixels is a click, not a drag.
const DRAG_THRESHOLD_PX = 4

/**
 * A room's day at a glance — open time tinted with the theme's own hue, outside hours
 * hatched, other people's bookings neutral, yours in the theme hue, closures in the
 * "blocked" state colour, the searched window outlined — and, with `pick`, a way to book
 * straight from it: drag across free time to choose any length (it snaps to the grid and
 * stops at bookings, closures and the room's maximum), click for the usual length, or
 * focus it and use ←/→ and Enter. All rows share one axis, so rooms line up by time.
 * Time runs in the reading direction: left to right, or right to left in Arabic (where →
 * steps back in time and ← forward, as the bar reads).
 */
export function DayBar({ axis, open, closed, busy, selection, label, pick }: DayBarProps) {
  const { t } = useTranslation()
  const trackRef = useRef<HTMLDivElement>(null)
  const drag = useRef<{ anchor: number; stretch: MinuteRange; startX: number; moved: boolean; range: MinuteRange } | null>(null)
  const [draft, setDraft] = useState<MinuteRange | null>(null)
  const [cursor, setCursor] = useState<number | null>(null)

  const span = Math.max(axis.to - axis.from, 1)

  const place = (startMinute: number, endMinute: number) => {
    const start = Math.max(startMinute, axis.from)
    const end = Math.min(endMinute, axis.to)
    return {
      insetInlineStart: `${((start - axis.from) / span) * 100}%`,
      width: `${(Math.max(end - start, 0) / span) * 100}%`,
    }
  }

  const isRtl = (el: Element) => getComputedStyle(el).direction === 'rtl'

  function minuteAt(clientX: number): number {
    const track = trackRef.current!
    const rect = track.getBoundingClientRect()
    const fromStart = isRtl(track) ? rect.right - clientX : clientX - rect.left
    const ratio = Math.min(Math.max(fromStart / Math.max(rect.width, 1), 0), 0.9999)
    return axis.from + ratio * span
  }

  function handlePointerDown(e: PointerEvent<HTMLDivElement>) {
    if (!pick || e.button !== 0) return
    const minute = minuteAt(e.clientX)
    const stretch = freeStretchAt(minute, pick.rules)
    if (!stretch) return

    e.currentTarget.setPointerCapture(e.pointerId)
    const range = clickRange(minute, pick.defaultLength, stretch, pick.rules)
    drag.current = { anchor: minute, stretch, startX: e.clientX, moved: false, range }
    setDraft(range)
  }

  function handlePointerMove(e: PointerEvent<HTMLDivElement>) {
    const d = drag.current
    if (!pick || !d) return
    if (Math.abs(e.clientX - d.startX) > DRAG_THRESHOLD_PX) d.moved = true
    if (d.moved) {
      d.range = dragRange(d.anchor, minuteAt(e.clientX), d.stretch, pick.rules)
      setDraft(d.range)
    }
  }

  function handlePointerUp() {
    const d = drag.current
    drag.current = null
    if (!pick || !d) return
    setDraft(null)
    pick.onPick(d.range)
  }

  function cancelDrag() {
    drag.current = null
    setDraft(null)
  }

  // Keyboard: a cursor that steps along free time slot by slot; Enter books the usual
  // length from it. The preview shows what Enter would book.
  const cursorRange = (() => {
    if (!pick || cursor === null) return null
    const stretch = freeStretchAt(cursor, pick.rules)
    return stretch ? clickRange(cursor, pick.defaultLength, stretch, pick.rules) : null
  })()

  function nextFree(from: number, step: number): number | null {
    for (let m = from; m >= axis.from && m < axis.to; m += step) {
      if (pick && freeStretchAt(m, pick.rules)) return m
    }
    return null
  }

  function handleFocus() {
    if (!pick || cursor !== null) return
    setCursor(nextFree(selection.startMinute, pick.rules.slotMinutes) ?? nextFree(axis.from, pick.rules.slotMinutes))
  }

  function handleKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (!pick || cursor === null) return
    const slot = pick.rules.slotMinutes
    if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
      e.preventDefault()
      // The arrow pointing the way the bar reads moves later.
      const later = (e.key === 'ArrowRight') !== isRtl(e.currentTarget)
      const step = later ? slot : -slot
      const next = nextFree(cursor + step, step)
      if (next !== null) setCursor(next)
    } else if (e.key === 'Enter' && cursorRange) {
      e.preventDefault()
      pick.onPick(cursorRange)
    }
  }

  const preview = draft ?? cursorRange

  const ticks: number[] = []
  for (let m = Math.ceil(axis.from / 120) * 120; m <= axis.to; m += 120) ticks.push(m)

  return (
    <div className="relative min-w-0" data-daybar>
      {preview && (
        <span
          className="pointer-events-none absolute -top-6 z-10 -translate-x-1/2 rounded-md bg-foreground px-1.5 py-0.5 font-mono text-[11px] whitespace-nowrap text-background rtl:translate-x-1/2"
          style={{ insetInlineStart: `${(((preview.start + preview.end) / 2 - axis.from) / span) * 100}%` }}
          aria-hidden="true"
        >
          {fromMinutes(preview.start)}–{fromMinutes(preview.end)}
        </span>
      )}

      <div
        ref={trackRef}
        className={cn(
          'relative h-6 overflow-hidden rounded-md pointer-coarse:h-9',
          pick && 'cursor-crosshair touch-pan-y outline-none select-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
        )}
        style={OUTSIDE_HOURS}
        {...(pick
          ? {
              role: 'group',
              tabIndex: 0,
              'aria-label': t('FindSpace:DayBarPick', { label, space: pick.roomName }),
              onPointerDown: handlePointerDown,
              onPointerMove: handlePointerMove,
              onPointerUp: handlePointerUp,
              onPointerCancel: cancelDrag,
              onFocus: handleFocus,
              onBlur: () => setCursor(null),
              onKeyDown: handleKeyDown,
            }
          : { role: 'img', 'aria-label': label })}
      >
        {open.map((r, i) => (
          <span key={`o${i}`} className="absolute inset-y-0 bg-slot-open" style={place(r.startMinute, r.endMinute)} />
        ))}
        {closed.map((r, i) => (
          <span key={`c${i}`} className="absolute inset-y-0 bg-slot-closed" style={place(r.startMinute, r.endMinute)} />
        ))}
        {busy.map((r, i) => (
          <span
            key={`b${i}`}
            className={cn('absolute inset-y-0', r.isMine ? 'bg-slot-mine' : 'bg-slot-busy')}
            style={place(r.startMinute, r.endMinute)}
          />
        ))}
        <span
          className="absolute inset-y-0 rounded-sm border-2 border-solid border-brand bg-brand/12"
          style={place(selection.startMinute, selection.endMinute)}
        />
        {preview && (
          <span
            className="absolute inset-y-0 rounded-sm border-2 border-solid border-brand bg-brand/40"
            style={place(preview.start, preview.end)}
          />
        )}
      </div>

      {preview && (
        <span className="sr-only" aria-live="polite">
          {t('FindSpace:DayBarRange', { start: fromMinutes(preview.start), end: fromMinutes(preview.end) })}
        </span>
      )}

      <div className="relative mt-0.5 h-3.5 font-mono text-[10px] text-muted-foreground" aria-hidden="true">
        {ticks.map((m) => (
          <span
            key={m}
            className="absolute -translate-x-1/2 rtl:translate-x-1/2"
            style={{ insetInlineStart: `${((m - axis.from) / span) * 100}%` }}
          >
            {fromMinutes(m).slice(0, 2)}
          </span>
        ))}
      </div>
    </div>
  )
}
