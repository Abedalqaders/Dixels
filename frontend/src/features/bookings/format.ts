import type { OperatingWindowDto } from '../space-management/api/spaceManagementApi'

// Display wording for booking rules — kept in step with the backend's BookingFormat so a
// rule reads the same in the space list as it does in a rejection message.

const SHORT_DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

/** "every day", a run like "Sun–Thu", or a list like "Sun, Tue, Thu" (0 = Sunday). */
export function formatDays(days: number[]): string {
  const sorted = [...days].sort((a, b) => a - b)
  if (sorted.length === 7) return 'every day'
  if (sorted.length === 0) return 'no days'
  if (sorted.length >= 3 && sorted[sorted.length - 1] - sorted[0] === sorted.length - 1) {
    return `${SHORT_DAYS[sorted[0]]}–${SHORT_DAYS[sorted[sorted.length - 1]]}`
  }
  return sorted.map((d) => SHORT_DAYS[d]).join(', ')
}

export function formatHours(hours: OperatingWindowDto): string {
  return hours.isOpen24Hours ? '24 hours' : `${hours.open}–${hours.close}`
}

export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h === 0) return `${m} min`
  if (m === 0) return `${h}h`
  return `${h}h ${m}m`
}
