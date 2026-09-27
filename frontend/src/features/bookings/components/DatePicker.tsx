import { useState } from 'react'
import { CalendarIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { formatDate } from '../../../lib/time/buildingTime'
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

/** A date field that opens a month calendar; dates outside the booking horizon can't be picked. */
export function DatePicker({ id, value, min, max, onChange }: DatePickerProps) {
  const [open, setOpen] = useState(false)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full justify-start font-mono font-normal">
          <CalendarIcon className="text-muted-foreground" />
          {formatDate(value)}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="single"
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
