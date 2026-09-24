// Minimum attendees — space-only, anti-waste rule. Port of the mock's sMinAtt/sCapNote/
// sMinAttNote (js/admin-constraints.js fillSpace).

interface AttendeesStepperProps {
  value: number | null
  capacity: number
  disabled: boolean
  onChange: (value: number | null) => void
}

export function AttendeesStepper({ value, capacity, disabled, onChange }: AttendeesStepperProps) {
  return (
    <div className="pair narrow">
      <input
        className="ctrl mono"
        type="number"
        min={1}
        value={value ?? ''}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
      />
      <span className="unit">of {capacity} seats</span>
    </div>
  )
}

export function attendeesStepperNote(value: number | null, capacity: number): string {
  if (value === null) {
    return 'No minimum — any group size can book. The capacity is still enforced as the maximum.'
  }
  if (value > capacity) {
    return `Invalid: a minimum above the ${capacity}-seat capacity makes this space unbookable.`
  }
  return `Rejects bookings for fewer than ${value} attendees.`
}
