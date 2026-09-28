import { useState } from 'react'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { IsoDate } from '@/lib/time/buildingTime'
import type { RecurrenceDto } from '@/features/bookings/api/bookingsApi'
import { describeRecurrence, Frequency, repeatPresets, weekdayOf } from '@/features/bookings/recurrence'
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
  const [customOpen, setCustomOpen] = useState(false)
  const presets = repeatPresets(date, openDays)

  function choose(choice: RepeatChoice) {
    if (choice === 'custom') {
      setCustomOpen(true)
      return
    }
    const preset = presets.find((p) => p.choice === choice)
    const frequency = preset?.rule ? preset.rule(date).frequency : Frequency.Weekly
    onChange({ choice, endDate: choice === 'none' ? null : value.endDate ?? defaultEnd(frequency), custom: null })
  }

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
        <Label htmlFor="bk-repeat">Repeat</Label>
        <Select value={value.choice} onValueChange={(v) => choose(v as RepeatChoice)}>
          <SelectTrigger id="bk-repeat" className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {presets.map((p) => (
              <SelectItem key={p.choice} value={p.choice}>
                {p.choice === 'custom' && value.choice === 'custom' ? 'Custom' : p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {rule && value.choice !== 'custom' && (
        <div className="grid gap-2">
          <Label htmlFor="bk-repeat-end">Ends</Label>
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
          {describeRecurrence(rule, date, openDays)}
          {value.choice === 'custom' && (
            <>
              {' · '}
              <button
                type="button"
                className="font-medium text-foreground underline underline-offset-4"
                onClick={() => setCustomOpen(true)}
              >
                Edit
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
