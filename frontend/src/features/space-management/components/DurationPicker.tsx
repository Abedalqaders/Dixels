// Maximum duration is booking policy, not physical access, so unlike days/hours it's a
// plain override with no narrow-only check against the parent (see CONSTRAINTS.md).
// Displayed in hours to match the mock, stored/submitted in minutes to match the API.

interface DurationPickerProps {
  hours: number
  disabled: boolean
  onChange: (hours: number) => void
}

export function DurationPicker({ hours, disabled, onChange }: DurationPickerProps) {
  return (
    <div className="pair narrow">
      <input
        className="ctrl mono"
        type="number"
        min={0.25}
        step={0.25}
        value={hours}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      <span className="unit">hours</span>
    </div>
  )
}

export function minutesToHours(minutes: number): number {
  return Math.round((minutes / 60) * 100) / 100
}

export function hoursToMinutes(hours: number): number {
  return Math.round(hours * 60)
}
