import { Calendar } from '@/components/ui/calendar'
import type { IsoDate } from '@/lib/time/buildingTime'

interface MiniCalendarProps {
  /** The day the main view is on. */
  selected: IsoDate
  /** The month the mini calendar shows (any day in it). */
  month: IsoDate
  today: IsoDate
  /** Days with at least one booking get a dot. */
  bookedDays: IsoDate[]
  onSelect: (date: IsoDate) => void
  onMonthChange: (month: IsoDate) => void
}

// react-day-picker works with JS Dates; only year/month/day are used, so a Date at local
// midnight safely carries a building-local calendar date (same trick as DatePicker).
function toDate(iso: IsoDate): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

function toIso(date: Date): IsoDate {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

/** A small month for jumping around, with a dot under every day that has a booking. */
export function MiniCalendar({ selected, month, today, bookedDays, onSelect, onMonthChange }: MiniCalendarProps) {
  return (
    <Calendar
      mode="single"
      className="p-1 [--cell-size:--spacing(8)]"
      selected={toDate(selected)}
      month={toDate(month)}
      today={toDate(today)}
      weekStartsOn={0}
      onMonthChange={(m) => onMonthChange(toIso(m))}
      onSelect={(date) => date && onSelect(toIso(date))}
      modifiers={{ booked: bookedDays.map(toDate) }}
      modifiersClassNames={{
        booked:
          "relative after:absolute after:bottom-1 after:left-1/2 after:size-1 after:-translate-x-1/2 after:rounded-full after:bg-brand after:content-['']",
      }}
    />
  )
}
