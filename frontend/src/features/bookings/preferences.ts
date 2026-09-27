import { toMinutes } from '../../lib/time/buildingTime'
import type { BookableSpaceDto } from './api/bookingsApi'

const LAST_DURATION_KEY = 'dixels.bookings.lastDurationMinutes'
const DAY_MINUTES = 24 * 60

/**
 * The length of the employee's last booking — people tend to book the same length every
 * time, so it becomes the default next time. A convenience only: browser storage can be
 * missing or blocked (private mode), in which case this quietly returns null.
 */
export function readLastDuration(): number | null {
  try {
    const value = Number(globalThis.localStorage?.getItem(LAST_DURATION_KEY))
    return Number.isInteger(value) && value > 0 && value <= DAY_MINUTES ? value : null
  } catch {
    return null
  }
}

export function rememberDuration(minutes: number): void {
  try {
    globalThis.localStorage?.setItem(LAST_DURATION_KEY, String(minutes))
  } catch {
    // Storage unavailable — nothing to remember, nothing breaks.
  }
}

/**
 * When a space closes, as minutes from midnight, for the "Until closing" choice. A 24h
 * space or one whose hours run past midnight "closes" at midnight as far as a same-day
 * booking is concerned.
 */
export function closingMinute(space: Pick<BookableSpaceDto, 'hours'>): number {
  const { isOpen24Hours, open, close } = space.hours.value
  if (isOpen24Hours) return DAY_MINUTES
  return toMinutes(close) > toMinutes(open) ? toMinutes(close) : DAY_MINUTES
}
