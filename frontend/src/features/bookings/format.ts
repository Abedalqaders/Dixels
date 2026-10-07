import i18n from '@/i18n'
import type { OperatingWindowDto } from '@/features/space-management/api/spaceManagementApi'
import { addDays } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { formatClockRange, formatWeekday } from '@/lib/time/format'

// Display wording for booking rules — kept in step with the backend's BookingFormat so a
// rule reads the same in the space list as it does in a rejection message. Weekday names
// come from lib/time/format, so they're in the reader's language ("Tue", "الثلاثاء").

/** A booking's title, or "Booking" in the reader's language when it was left blank (stored empty). */
export function bookingTitle(title: string): string {
  return title || i18n.t('Booking:Untitled')
}

/** 4 Jan 2026 was a Sunday, so day `d` of the week (0 = Sunday) is this date plus d. */
const A_SUNDAY: IsoDate = '2026-01-04'

/** A day of the week (0 = Sunday) by name: "Tuesday", "Tue" or "T" in English. */
export function weekdayName(day: number, style: 'long' | 'short' | 'narrow' = 'long'): string {
  return formatWeekday(addDays(A_SUNDAY, day), style)
}

/** "every day", a run like "Sun–Thu", or a list like "Sun, Tue, Thu" (0 = Sunday). */
export function formatDays(days: number[]): string {
  const sorted = [...days].sort((a, b) => a - b)
  if (sorted.length === 7) return i18n.t('Booking:EveryDay')
  if (sorted.length === 0) return i18n.t('Booking:NoDays')
  if (sorted.length >= 3 && sorted[sorted.length - 1] - sorted[0] === sorted.length - 1) {
    return i18n.t('Booking:DayRange', {
      from: weekdayName(sorted[0], 'short'),
      to: weekdayName(sorted[sorted.length - 1], 'short'),
    })
  }
  return sorted.map((d) => weekdayName(d, 'short')).join(i18n.t('Booking:ListSeparator'))
}

/** "Sunday, Tuesday and Thursday" — the last two joined by the language's "and". */
export function joinNames(names: string[]): string {
  if (names.length <= 1) return names.join('')
  return i18n.t('Booking:ListAnd', {
    list: names.slice(0, -1).join(i18n.t('Booking:ListSeparator')),
    last: names[names.length - 1],
  })
}

export function formatHours(hours: OperatingWindowDto): string {
  return hours.isOpen24Hours ? i18n.t('Booking:Open24Hours') : formatClockRange(hours.open, hours.close)
}

/** "45 min", "2h", "1h 30m" — and in the reader's language ("ساعتان و30 دقيقة"). */
export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h === 0) return i18n.t('Duration:Minutes', { count: m })
  if (m === 0) return i18n.t('Duration:Hours', { count: h })
  return i18n.t('Duration:HoursMinutes', {
    hours: i18n.t('Duration:Hours', { count: h }),
    minutes: i18n.t('Duration:MinutesShort', { count: m }),
  })
}
