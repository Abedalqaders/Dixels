import { Repeat } from 'lucide-react'
import { cn } from '@/lib/utils'
import { addDays, timeOf } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { dayOfMonth, monthGrid, startOfMonth } from '@/features/calendar/calendarDates'

// More than this per day and the rest fold into "+N more".
const MAX_CHIPS = 3
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

interface MonthGridProps {
  /** Any day in the month to show. */
  date: IsoDate
  bookings: BookingDto[]
  today: IsoDate
  onOpenBooking: (booking: BookingDto) => void
  /** Clicking a day (or its "+N more") opens it in the Day view. */
  onOpenDay: (date: IsoDate) => void
}

/** The Month view: a week per row, each day listing its first few bookings by start time. */
export function MonthGrid({ date, bookings, today, onOpenBooking, onOpenDay }: MonthGridProps) {
  const { start, weeks } = monthGrid(date)
  const month = startOfMonth(date).slice(0, 7)
  const cells = Array.from({ length: weeks * 7 }, (_, i) => addDays(start, i))

  return (
    <div className="overflow-hidden rounded-xl border bg-card">
      <div className="grid grid-cols-7 border-b">
        {WEEKDAYS.map((d) => (
          <span key={d} className="py-2 text-center text-xs tracking-wide text-muted-foreground uppercase">
            {d}
          </span>
        ))}
      </div>
      <div className="grid grid-cols-7" style={{ gridTemplateRows: `repeat(${weeks}, minmax(110px, 1fr))` }}>
        {cells.map((d, i) => {
          const dayBookings = bookings.filter((b) => b.localStart.startsWith(d))
          const inMonth = d.startsWith(month)
          const hidden = dayBookings.length - MAX_CHIPS
          return (
            <div
              key={d}
              className={cn(
                'flex min-w-0 cursor-pointer flex-col gap-1 border-b p-1.5 hover:bg-muted/50',
                i % 7 !== 0 && 'border-l',
                i >= (weeks - 1) * 7 && 'border-b-0',
                !inMonth && 'bg-muted/40',
              )}
              onClick={() => onOpenDay(d)}
            >
              <button
                type="button"
                className={cn(
                  'grid size-7 place-items-center self-start rounded-full text-sm font-medium hover:bg-muted',
                  !inMonth && 'text-muted-foreground',
                  d === today && 'bg-brand text-primary-foreground hover:bg-brand',
                )}
                aria-label={`Open ${d}`}
                aria-current={d === today ? 'date' : undefined}
                onClick={(e) => {
                  e.stopPropagation()
                  onOpenDay(d)
                }}
              >
                {dayOfMonth(d)}
              </button>
              {dayBookings.slice(0, MAX_CHIPS).map((b) => (
                <button
                  key={b.id}
                  type="button"
                  className={cn(
                    'flex min-w-0 items-center gap-1.5 rounded px-1.5 py-0.5 text-left text-xs hover:bg-slot-open',
                    d < today && 'opacity-60',
                    b.status === 'Cancelled' && 'text-muted-foreground line-through',
                  )}
                  aria-label={`${b.status === 'Cancelled' ? 'Cancelled: ' : ''}${b.title}, ${timeOf(b.localStart)}–${timeOf(b.localEnd)}, ${b.spaceName}`}
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpenBooking(b)
                  }}
                >
                  <span className="size-1.5 flex-none rounded-full bg-brand" aria-hidden="true" />
                  <span className="font-mono text-[11px] text-muted-foreground">{timeOf(b.localStart)}</span>
                  <span className="truncate font-medium">{b.title}</span>
                  {b.seriesId && <Repeat className="size-3 flex-none text-muted-foreground" aria-label="Repeats" />}
                </button>
              ))}
              {hidden > 0 && (
                <button
                  type="button"
                  className="self-start rounded px-1.5 text-xs font-medium text-muted-foreground hover:text-foreground"
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpenDay(d)
                  }}
                >
                  +{hidden} more
                </button>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
