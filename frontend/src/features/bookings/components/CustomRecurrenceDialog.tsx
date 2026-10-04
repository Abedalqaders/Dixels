import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { formatDate } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto, RecurrenceFrequency } from '@/features/bookings/api/bookingsApi'
import { describeRecurrence, Frequency, MonthlyRepeat, weekdayOf, weekdayPosition } from '@/features/bookings/recurrence'
import { weekdayName } from '@/features/bookings/format'
import { DatePicker } from './DatePicker'

const WEEK = [0, 1, 2, 3, 4, 5, 6]
const MAX_INTERVAL = 99
/** "day" / "days" — the unit after the interval, in the form its number needs. */
const UNITS = {
  [Frequency.Daily]: 'Repeat:UnitDay',
  [Frequency.Weekly]: 'Repeat:UnitWeek',
  [Frequency.Monthly]: 'Repeat:UnitMonth',
} as const

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
  const { t } = useTranslation()
  const [rule, setRule] = useState<RecurrenceDto>(() => ({
    ...initial,
    weekdays: initial.weekdays.length ? initial.weekdays : [weekdayOf(date)],
  }))
  const noDays = rule.frequency === Frequency.Weekly && rule.weekdays.length === 0
  const badInterval = !Number.isInteger(rule.interval) || rule.interval < 1 || rule.interval > MAX_INTERVAL

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
          <DialogTitle>{t('Repeat:CustomTitle')}</DialogTitle>
          <DialogDescription>{t('Repeat:Starting', { date: formatDate(date) })}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="rr-interval">{t('Repeat:Every')}</Label>
            <div className="flex gap-2">
              <Input
                id="rr-interval"
                type="number"
                className="w-20 font-mono"
                min={1}
                max={MAX_INTERVAL}
                value={Number.isNaN(rule.interval) ? '' : rule.interval}
                onChange={(e) => setRule({ ...rule, interval: e.target.valueAsNumber })}
                aria-invalid={badInterval}
              />
              <Select value={String(rule.frequency)} onValueChange={(v) => setRule({ ...rule, frequency: Number(v) as RecurrenceFrequency })}>
                <SelectTrigger className="w-36" aria-label={t('Repeat:Unit')}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[Frequency.Daily, Frequency.Weekly, Frequency.Monthly].map((f) => (
                    <SelectItem key={f} value={String(f)}>
                      {t(UNITS[f], { count: rule.interval })}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {badInterval && (
              <p className="text-xs text-destructive">
                {t('Repeat:IntervalRange', { max: MAX_INTERVAL, unit: t(UNITS[rule.frequency], { count: MAX_INTERVAL }) })}
              </p>
            )}
          </div>

          {rule.frequency === Frequency.Weekly && (
            <fieldset className="grid gap-2">
              <legend className="mb-2 text-sm font-medium">{t('Repeat:On')}</legend>
              <div className="flex gap-1.5">
                {WEEK.map((day) => {
                  const on = rule.weekdays.includes(day)
                  const name = weekdayName(day)
                  return (
                    <button
                      key={day}
                      type="button"
                      aria-pressed={on}
                      aria-label={name}
                      title={openDays.includes(day) ? name : t('Repeat:DayClosed', { weekday: name })}
                      onClick={() => toggleDay(day)}
                      className={cn(
                        'grid size-9 place-items-center rounded-full border text-sm font-semibold transition-colors',
                        'focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
                        on ? 'border-brand bg-brand text-primary-foreground' : 'bg-card hover:bg-muted',
                        !openDays.includes(day) && !on && 'text-muted-foreground',
                      )}
                    >
                      {weekdayName(day, 'narrow')}
                    </button>
                  )
                })}
              </div>
              {noDays && (
                <p role="alert" className="text-xs text-destructive">
                  {t('Repeat:PickADay')}
                </p>
              )}
            </fieldset>
          )}

          {rule.frequency === Frequency.Monthly && (
            <fieldset className="grid gap-2 text-sm">
              <legend className="mb-2 font-medium">{t('Repeat:MonthlyOn')}</legend>
              {[
                { value: MonthlyRepeat.OnDay, label: t('Repeat:DayN', { day: Number(date.slice(8)) }) },
                { value: MonthlyRepeat.OnWeekday, label: t('Repeat:OnPosition', { position: weekdayPosition(date) }) },
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
            <Label htmlFor="rr-end">{t('Repeat:End')}</Label>
            <DatePicker id="rr-end" value={rule.endDate} min={date} max={lastDate} onChange={(endDate) => setRule({ ...rule, endDate })} />
          </div>

          {!noDays && !badInterval && (
            <p className="text-sm text-muted-foreground">{describeRecurrence(rule, date, openDays)}</p>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onCancel}>
            {t('Common:Cancel')}
          </Button>
          <Button type="button" disabled={noDays || badInterval} onClick={() => onSave({ ...rule, weekdays: rule.frequency === Frequency.Weekly ? rule.weekdays : [] })}>
            {t('Common:Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
