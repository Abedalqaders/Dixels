import { useTranslation } from 'react-i18next'
import i18n from '@/i18n'

// Minimum attendees — space-only, anti-waste rule. Port of the mock's sMinAtt/sCapNote/
// sMinAttNote (js/admin-constraints.js fillSpace).

interface AttendeesStepperProps {
  value: number | null
  capacity: number
  disabled: boolean
  onChange: (value: number | null) => void
}

export function AttendeesStepper({ value, capacity, disabled, onChange }: AttendeesStepperProps) {
  const { t } = useTranslation()
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
      <span className="unit">{t('Rules:OfSeats', { count: capacity })}</span>
    </div>
  )
}

export function attendeesStepperNote(value: number | null, capacity: number): string {
  if (value === null) return i18n.t('Rules:NoMinimumNote')
  if (value > capacity) return i18n.t('Rules:MinAboveCapacity', { capacity })
  return i18n.t('Rules:MinAttendeesNote', { count: value })
}
