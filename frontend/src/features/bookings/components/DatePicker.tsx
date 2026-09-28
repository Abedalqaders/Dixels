import { useState } from 'react'
import { format } from 'date-fns'
import { ChevronDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { IsoDate } from '../../../lib/time/buildingTime'

interface DatePickerProps {
  id: string
  value: IsoDate
  /** First and last bookable dates — everything outside is disabled in the calendar. */
  min: IsoDate
  max: IsoDate
  onChange: (date: IsoDate) => void
}

// The calendar works with JS Dates; only the year/month/day are used, so a Date at local
// midnight is a safe carrier for a building-local calendar date.
function toDate(iso: IsoDate): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

function toIso(date: Date): IsoDate {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

/** shadcn's basic date picker — a button that opens a plain month calendar (‹ › arrows),
 * limited to the booking horizon: dates outside it are disabled and the arrows stop at its
 * first and last months. */
export function DatePicker({ id, value, min, max, onChange }: DatePickerProps) {
  const [open, setOpen] = useState(false)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full justify-between text-left font-normal">
          {format(toDate(value), 'PPP')}
          <ChevronDownIcon className="text-muted-foreground" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto overflow-hidden p-0" align="start">
        <Calendar
          mode="single"
          startMonth={toDate(min)}
          endMonth={toDate(max)}
          selected={toDate(value)}
          defaultMonth={toDate(value)}
          disabled={[{ before: toDate(min) }, { after: toDate(max) }]}
          onSelect={(date) => {
            if (date) {
              onChange(toIso(date))
              setOpen(false)
            }
          }}
        />
      </PopoverContent>
    </Popover>
  )
}
