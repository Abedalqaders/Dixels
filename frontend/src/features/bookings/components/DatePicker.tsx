import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { IsoDate } from '@/lib/time/buildingTime'
import { formatDay, formatMonthYear, formatWeekday } from '@/lib/time/format'
import { languageInfo } from '@/i18n'

interface DatePickerProps {
  id: string
  value: IsoDate
  /** First and last bookable dates — everything outside is disabled in the calendar. */
  min: IsoDate
  max: IsoDate
  /** Also greys out these dates inside min…max — e.g. ones a room has no free time on. */
  isDisabled?: (date: IsoDate) => boolean
  onChange: (date: IsoDate) => void
  /** The id of a message under the field about a problem with the date; marks the field invalid. */
  errorId?: string
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
 * limited to the booking horizon: dates outside it (and any `isDisabled` says no to) are
 * disabled and the arrows stop at its first and last months. Month and weekday names (and what a screen reader hears) come
 * from the app's own formatting, in the reader's language; the month runs right to left
 * in Arabic. */
export function DatePicker({ id, value, min, max, isDisabled, onChange, errorId }: DatePickerProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          variant="outline"
          className="w-full justify-between text-start font-normal"
          aria-invalid={errorId ? true : undefined}
          aria-describedby={errorId}
        >
          {formatDay(value, 'long')}
          <ChevronDownIcon className="text-muted-foreground" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto overflow-hidden p-0" align="start">
        <Calendar
          mode="single"
          dir={languageInfo().dir}
          formatters={{
            formatCaption: (m) => formatMonthYear(toIso(m)),
            formatWeekdayName: (d) => formatWeekday(toIso(d), 'narrow'),
          }}
          labels={{
            labelPrevious: () => t('Calendar:PreviousMonth'),
            labelNext: () => t('Calendar:NextMonth'),
            labelGrid: (m) => formatMonthYear(toIso(m)),
            labelWeekday: (d) => formatWeekday(toIso(d), 'long'),
            labelDayButton: (d) => formatDay(toIso(d), 'long'),
          }}
          weekStartsOn={0}
          startMonth={toDate(min)}
          endMonth={toDate(max)}
          selected={toDate(value)}
          defaultMonth={toDate(value)}
          disabled={[{ before: toDate(min) }, { after: toDate(max) }, ...(isDisabled ? [(d: Date) => isDisabled(toIso(d))] : [])]}
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
