import { useTranslation } from 'react-i18next'
import { allowedDays, dayLabel, OperatingDays } from '@/features/space-management/operatingDays'
import type { DayName } from '@/features/space-management/operatingDays'

// Port of the mock's renderChips/dayOptions (js/admin-constraints.js), but proactive
// rather than reactive: days outside the parent's own set are rendered disabled instead
// of accepting the click and then rejecting it with a toast.

const ALL_DAYS: DayName[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

interface DayChipPickerProps {
  value: OperatingDays
  parent: OperatingDays
  disabled: boolean
  onChange: (next: OperatingDays) => void
}

export function DayChipPicker({ value, parent, disabled, onChange }: DayChipPickerProps) {
  // The chips' names are in the reader's language (dayLabel): re-render when it changes.
  useTranslation()
  const allowed = new Set(allowedDays(parent))

  function toggle(day: DayName) {
    if (disabled || !allowed.has(day)) return
    const next = value.contains(day) ? withoutDay(value, day) : withDay(value, day)
    onChange(next)
  }

  return (
    <div className="chipset" data-locked={disabled ? 'true' : 'false'}>
      {ALL_DAYS.map((day) => (
        <button
          key={day}
          type="button"
          className={value.contains(day) ? 'on' : undefined}
          disabled={disabled || !allowed.has(day)}
          onClick={() => toggle(day)}
        >
          {dayLabel(day)}
        </button>
      ))}
    </div>
  )
}

function withDay(days: OperatingDays, day: DayName): OperatingDays {
  return OperatingDays.fromDayNames([...days.toDayNames(), day])
}

function withoutDay(days: OperatingDays, day: DayName): OperatingDays {
  return OperatingDays.fromDayNames(days.toDayNames().filter((d) => d !== day))
}
