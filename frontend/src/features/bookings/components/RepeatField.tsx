import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import {
  describeRecurrence,
  endAfterMonths,
  endAfterWeeks,
  Frequency,
  maxMonths,
  maxWeeks,
  monthsUntil,
  repeatPresets,
  weekdayOf,
  weeksUntil,
} from '@/features/bookings/recurrence'
import type { RepeatChoice, RepeatValue } from '@/features/bookings/recurrence'
import { CustomRecurrenceDialog } from './CustomRecurrenceDialog'
import { DatePicker } from './DatePicker'

interface RepeatFieldProps {
  date: IsoDate
  openDays: number[]
  /** The last end date a series may have (the building's series horizon). */
  lastDate: IsoDate
  value: RepeatValue
  /** The rule `value` stands for on `date` — worked out by the form. */
  rule: RecurrenceDto | null
  defaultEnd: (frequency: number) => IsoDate
  onChange: (value: RepeatValue) => void
}

/**
 * Teams' Repeat dropdown: quick choices worded from the date ("Weekly on Tuesday"), an end
 * date, the "Occurs every…" sentence — and "Custom…", which opens the full dialog.
 */
export function RepeatField({ date, openDays, lastDate, value, rule, defaultEnd, onChange }: RepeatFieldProps) {
  const { t } = useTranslation()
  const [customOpen, setCustomOpen] = useState(false)
  const presets = repeatPresets(date, openDays)

  function choose(choice: RepeatChoice) {
    if (choice === 'custom') {
      setCustomOpen(true)
      return
    }
    const preset = presets.find((p) => p.choice === choice)
    const frequency = preset?.rule ? preset.rule(date).frequency : Frequency.Weekly
    let endDate = choice === 'none' ? null : value.endDate ?? defaultEnd(frequency)
    // Counted choices end on their last occurrence, so "until …" names a real date.
    if (endDate && choice === 'weekly') endDate = endAfterWeeks(date, weeksUntil(date, endDate), lastDate)
    if (endDate && choice === 'monthly') endDate = endAfterMonths(date, monthsUntil(date, endDate), lastDate)
    onChange({ choice, endDate, custom: null })
  }

  // Quick choices that are counted instead of ending on a date.
  const count =
    rule && value.choice === 'weekly'
      ? {
          label: 'Repeat:NumberOfWeeks' as const,
          units: 'Repeat:WeeksUpTo' as const,
          value: weeksUntil(date, rule.endDate),
          max: maxWeeks(date, lastDate),
          endAfter: (n: number) => endAfterWeeks(date, n, lastDate),
        }
      : rule && value.choice === 'monthly'
        ? {
            label: 'Repeat:NumberOfMonths' as const,
            units: 'Repeat:MonthsUpTo' as const,
            value: monthsUntil(date, rule.endDate),
            max: maxMonths(date, lastDate),
            endAfter: (n: number) => endAfterMonths(date, n, lastDate),
          }
        : null

  const customStart: RecurrenceDto = rule ?? {
    frequency: Frequency.Weekly,
    interval: 1,
    weekdays: [weekdayOf(date)],
    monthlyRepeat: 0,
    endDate: defaultEnd(Frequency.Weekly),
  }

  return (
    <div className="grid gap-3 sm:grid-cols-[1fr_1fr]">
      <div className="grid gap-2">
        <Label htmlFor="bk-repeat">{t('Repeat:Label')}</Label>
        <Select value={value.choice} onValueChange={(v) => choose(v as RepeatChoice)}>
          <SelectTrigger id="bk-repeat" className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {presets.map((p) => (
              <SelectItem key={p.choice} value={p.choice}>
                {p.choice === 'custom' && value.choice === 'custom' ? t('Repeat:Custom') : p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Weekly and monthly count occurrences ("for 6 weeks", "for 3 months") rather than
          asking for an end date: it's how people think about a standing slot. It's still
          sent as an end date — the day of the last occurrence. */}
      {rule && count && (
        <div className="grid gap-2">
          <Label htmlFor="bk-repeat-count">{t(count.label)}</Label>
          <div className="flex items-center gap-2">
            <Input
              id="bk-repeat-count"
              type="number"
              min={1}
              max={count.max}
              className="w-24 font-mono"
              value={count.value}
              onChange={(e) => {
                const n = e.target.valueAsNumber
                if (Number.isInteger(n) && n >= 1) onChange({ ...value, endDate: count.endAfter(n) })
              }}
            />
            <span className="text-sm text-muted-foreground">
              {t(count.units, { count: count.value, max: count.max })}
            </span>
          </div>
        </div>
      )}

      {rule && value.choice !== 'custom' && !count && (
        <div className="grid gap-2">
          <Label htmlFor="bk-repeat-end">{t('Repeat:Ends')}</Label>
          <DatePicker
            id="bk-repeat-end"
            value={rule.endDate}
            min={date}
            max={lastDate}
            onChange={(endDate) => onChange({ ...value, endDate })}
          />
        </div>
      )}

      {rule && (
        <p className="text-sm text-muted-foreground sm:col-span-2">
          {/* Counted: say "until" the last occurrence, even if the date moved since. */}
          {describeRecurrence(count ? { ...rule, endDate: count.endAfter(count.value) } : rule, date, openDays)}
          {value.choice === 'custom' && (
            <>
              {' · '}
              <button
                type="button"
                className="font-medium text-foreground underline underline-offset-4"
                onClick={() => setCustomOpen(true)}
              >
                {t('Common:Edit')}
              </button>
            </>
          )}
        </p>
      )}

      {customOpen && (
        <CustomRecurrenceDialog
          date={date}
          lastDate={lastDate}
          initial={customStart}
          openDays={openDays}
          onCancel={() => setCustomOpen(false)}
          onSave={(custom) => {
            setCustomOpen(false)
            onChange({ choice: 'custom', endDate: null, custom })
          }}
        />
      )}
    </div>
  )
}
