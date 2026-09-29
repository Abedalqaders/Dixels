import { Plus, Repeat } from 'lucide-react'
import { cn } from '@/lib/utils'
import { addDays, timeOf } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { dayOfMonth, monthGrid, startOfMonth } from '@/features/calendar/calendarDates'
import { formatClock } from '@/lib/time/format'
import type { CalendarItem } from '@/features/calendar/calendarItem'

// More than this per day and the rest fold into "N more…" (chips) or "+N" (dots).
const MAX_CHIPS = 3
const MAX_DOTS = 3
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

interface MonthGridProps {
  /** Any day in the month to show. */
  date: IsoDate
  items: CalendarItem[]
  today: IsoDate
  /** Narrow screens: a dot per item instead of a chip, and the whole day opens on tap. */
  compact?: boolean
  onOpenItem: (item: CalendarItem) => void
  /** Clicking a day (or its "N more…") opens it in the Day view. */
  onOpenDay: (date: IsoDate) => void
  /** A day's "+", shown on hover — books straight from the month view. Omitted when read-only. */
  onQuickBook?: (date: IsoDate) => void
}

/**
 * The Month view: a week per row. On a wide screen each day lists its first few items as
 * chips — title on the left, start time on the right; on a narrow one, as dots, since
 * there's no room for words and the Day view is one tap away.
 */
export function MonthGrid({ date, items, today, compact = false, onOpenItem, onOpenDay, onQuickBook }: MonthGridProps) {
  const { start, weeks } = monthGrid(date)
  const month = startOfMonth(date).slice(0, 7)
  const cells = Array.from({ length: weeks * 7 }, (_, i) => addDays(start, i))

  return (
    <div>
      <div className="grid grid-cols-7 border-b">
        {WEEKDAYS.map((d) => (
          <span key={d} className="py-2.5 text-center text-xs text-muted-foreground">
            {compact ? d[0] : d}
          </span>
        ))}
      </div>
      <div className="grid grid-cols-7" style={{ gridTemplateRows: `repeat(${weeks}, minmax(${compact ? 72 : 110}px, 1fr))` }}>
        {cells.map((d, i) => {
          const dayItems = items.filter((b) => b.localStart.startsWith(d))
          const inMonth = d.startsWith(month)
          const cellClass = cn(
            'flex min-w-0 flex-col gap-1 border-b bg-transparent p-1.5',
            i % 7 !== 0 && 'border-l',
            i >= (weeks - 1) * 7 && 'border-b-0',
            !inMonth && 'bg-muted/40',
          )
          const number = (
            <span
              className={cn(
                'grid size-6 place-items-center rounded-full text-xs font-semibold',
                !inMonth && 'text-muted-foreground',
                d === today && 'bg-foreground text-background',
              )}
              aria-current={d === today ? 'date' : undefined}
            >
              {dayOfMonth(d)}
            </span>
          )

          if (compact) {
            const count = dayItems.length
            const hidden = count - MAX_DOTS
            return (
              <button
                key={d}
                type="button"
                className={cn(cellClass, 'cursor-pointer items-start text-left hover:bg-muted/50')}
                aria-label={`Open ${d}${count ? `, ${count} ${count === 1 ? 'booking' : 'bookings'}` : ''}`}
                onClick={() => onOpenDay(d)}
              >
                {number}
                {count > 0 && (
                  <span className="flex items-center gap-1" aria-hidden="true">
                    {dayItems.slice(0, MAX_DOTS).map((b) => (
                      <span key={b.id} className={cn('size-1.5 rounded-full', b.cancelled ? 'bg-muted-foreground/50' : 'bg-brand')} />
                    ))}
                    {hidden > 0 && <span className="text-[10px] leading-none text-muted-foreground">+{hidden}</span>}
                  </span>
                )}
              </button>
            )
          }

          const hidden = dayItems.length - MAX_CHIPS
          return (
            <div key={d} className={cn(cellClass, 'group cursor-pointer hover:bg-muted/50')} onClick={() => onOpenDay(d)}>
              <div className="flex items-center justify-between">
                <button
                  type="button"
                  className="self-start rounded-full bg-transparent hover:bg-muted"
                  aria-label={`Open ${d}`}
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpenDay(d)
                  }}
                >
                  {number}
                </button>
                {onQuickBook && d >= today && (
                  <button
                    type="button"
                    className="grid size-6 flex-none place-items-center rounded-full text-muted-foreground opacity-0 group-hover:opacity-100 hover:bg-muted hover:text-foreground focus-visible:opacity-100 pointer-coarse:size-9 pointer-coarse:opacity-100"
                    aria-label={`Book a room on ${d}`}
                    onClick={(e) => {
                      e.stopPropagation()
                      onQuickBook(d)
                    }}
                  >
                    <Plus className="size-3.5" />
                  </button>
                )}
              </div>
              {dayItems.slice(0, MAX_CHIPS).map((b) => (
                <button
                  key={b.id}
                  type="button"
                  className={cn(
                    'flex w-full min-w-0 items-center gap-1 rounded-md border border-brand/30 bg-slot-open px-1.5 py-0.5 text-left text-[11px] text-brand hover:border-brand/70',
                    d < today && 'opacity-60',
                    b.cancelled && 'border-dashed border-muted-foreground/50 bg-muted text-muted-foreground line-through',
                  )}
                  aria-label={`${b.cancelled ? 'Cancelled: ' : ''}${b.title}, ${timeOf(b.localStart)}–${timeOf(b.localEnd)}, ${b.location}`}
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpenItem(b)
                  }}
                >
                  {b.repeats && <Repeat className="size-3 flex-none" aria-label="Repeats" />}
                  <span className="min-w-0 flex-1 truncate font-semibold">{b.title}</span>
                  <span className="flex-none whitespace-nowrap opacity-70">{formatClock(timeOf(b.localStart))}</span>
                </button>
              ))}
              {hidden > 0 && (
                <button
                  type="button"
                  className="self-start bg-transparent px-1.5 text-[11px] font-medium text-muted-foreground hover:text-foreground"
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpenDay(d)
                  }}
                >
                  {hidden} more…
                </button>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
