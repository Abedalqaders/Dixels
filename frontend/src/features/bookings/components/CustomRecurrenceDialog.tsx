import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { formatDate } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import { describeRecurrence, Frequency, MonthlyRepeat, weekdayOf, weekdayPosition } from '@/features/bookings/recurrence'
import { DatePicker } from './DatePicker'

const DAY_LETTERS = ['S', 'M', 'T', 'W', 'T', 'F', 'S']
const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const UNITS: Record<number, [string, string]> = {
  [Frequency.Daily]: ['day', 'days'],
  [Frequency.Weekly]: ['week', 'weeks'],
  [Frequency.Monthly]: ['month', 'months'],
}

interface CustomRecurrenceDialogProps {
  /** The first date, which the pattern hangs off ("day 29", "the 2nd Tuesday"). */
  date: IsoDate
  /** The last end date a series may have (the building's series horizon). */
  lastDate: IsoDate
  initial: RecurrenceDto
  openDays: number[]
  onSave: (rule: RecurrenceDto) => void
  onCancel: () => void
}

/**
 * Teams' "Custom recurrence": Repeat every [N] [day / week / month], which weekdays (weekly),
 * on day 29 or on the 2nd Tuesday (monthly), and an end date. Weekly with no day picked
 * is pointed out rather than guessed.
 */
export function CustomRecurrenceDialog({ date, lastDate, initial, openDays, onSave, onCancel }: CustomRecurrenceDialogProps) {
  const [rule, setRule] = useState<RecurrenceDto>(() => ({
    ...initial,
    weekdays: initial.weekdays.length ? initial.weekdays : [weekdayOf(date)],
  }))
  const noDays = rule.frequency === Frequency.Weekly && rule.weekdays.length === 0
  const badInterval = !Number.isInteger(rule.interval) || rule.interval < 1 || rule.interval > 99
  const [, many] = UNITS[rule.frequency]

  function toggleDay(day: number) {
    setRule((r) => ({
      ...r,
      weekdays: r.weekdays.includes(day) ? r.weekdays.filter((d) => d !== day) : [...r.weekdays, day].sort((a, b) => a - b),
    }))
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Custom recurrence</DialogTitle>
          <DialogDescription>Starting {formatDate(date)}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="rr-interval">Repeat every</Label>
            <div className="flex gap-2">
              <Input
                id="rr-interval"
                type="number"
                className="w-20 font-mono"
                min={1}
                max={99}
                value={Number.isNaN(rule.interval) ? '' : rule.interval}
                onChange={(e) => setRule({ ...rule, interval: e.target.valueAsNumber })}
                aria-invalid={badInterval}
              />
              <Select value={String(rule.frequency)} onValueChange={(v) => setRule({ ...rule, frequency: Number(v) })}>
                <SelectTrigger className="w-36" aria-label="Unit">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[Frequency.Daily, Frequency.Weekly, Frequency.Monthly].map((f) => (
                    <SelectItem key={f} value={String(f)}>
                      {rule.interval === 1 ? UNITS[f][0] : UNITS[f][1]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {badInterval && <p className="text-xs text-destructive">Repeat every 1 to 99 {many}.</p>}
          </div>

          {rule.frequency === Frequency.Weekly && (
            <fieldset className="grid gap-2">
              <legend className="mb-2 text-sm font-medium">Repeat on</legend>
              <div className="flex gap-1.5">
                {DAY_LETTERS.map((letter, day) => {
                  const on = rule.weekdays.includes(day)
                  return (
                    <button
                      key={day}
                      type="button"
                      aria-pressed={on}
                      aria-label={DAY_NAMES[day]}
                      title={openDays.includes(day) ? DAY_NAMES[day] : `${DAY_NAMES[day]} — the room is closed`}
                      onClick={() => toggleDay(day)}
                      className={cn(
                        'grid size-9 place-items-center rounded-full border text-sm font-semibold transition-colors',
                        'focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
                        on ? 'border-brand bg-brand text-primary-foreground' : 'bg-card hover:bg-muted',
                        !openDays.includes(day) && !on && 'text-muted-foreground',
                      )}
                    >
                      {letter}
                    </button>
                  )
                })}
              </div>
              {noDays && (
                <p role="alert" className="text-xs text-destructive">
                  Pick at least one day.
                </p>
              )}
            </fieldset>
          )}

          {rule.frequency === Frequency.Monthly && (
            <fieldset className="grid gap-2 text-sm">
              <legend className="mb-2 font-medium">On</legend>
              {[
                { value: MonthlyRepeat.OnDay, label: `Day ${Number(date.slice(8))}` },
                { value: MonthlyRepeat.OnWeekday, label: `On ${weekdayPosition(date)}` },
              ].map((o) => (
                <label key={o.value} className="flex cursor-pointer items-center gap-2">
                  <input
                    type="radio"
                    name="rr-monthly"
                    className="size-4 accent-[var(--focus-ring)]"
                    checked={rule.monthlyRepeat === o.value}
                    onChange={() => setRule({ ...rule, monthlyRepeat: o.value })}
                  />
                  {o.label}
                </label>
              ))}
            </fieldset>
          )}

          <div className="grid gap-2">
            <Label htmlFor="rr-end">End</Label>
            <DatePicker id="rr-end" value={rule.endDate} min={date} max={lastDate} onChange={(endDate) => setRule({ ...rule, endDate })} />
          </div>

          {!noDays && !badInterval && (
            <p className="text-sm text-muted-foreground">{describeRecurrence(rule, date, openDays)}</p>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="button" disabled={noDays || badInterval} onClick={() => onSave({ ...rule, weekdays: rule.frequency === Frequency.Weekly ? rule.weekdays : [] })}>
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
