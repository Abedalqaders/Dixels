import { useCallback, useEffect, useRef, useState } from 'react'
import { addDays } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { getMyBookings } from '@/features/bookings/api/bookingsApi'
import { addMonths, monthGrid } from '@/features/calendar/calendarDates'
import { bookingItem } from '@/features/calendar/calendarItem'
import type { CalendarItem } from '@/features/calendar/calendarItem'

/** Which neighbouring months to load in the background: the ones a step would reach next. */
export type Prefetch = 'none' | 'prev' | 'next' | 'both'

/**
 * My calendar's items, one month grid at a time (the Sunday before the 1st to the Saturday
 * after the last day, ≤ 42 days — one request, however the page is looking at it). Loaded
 * months stay in memory, and a neighbouring month can be fetched in the background, so
 * stepping into it shows data straight away. `clear()` drops everything, for after a
 * booking is made or cancelled.
 */
export class MonthBookingsCache {
  private readonly loaded = new Map<IsoDate, CalendarItem[]>()
  private readonly pending = new Map<IsoDate, Promise<CalendarItem[]>>()
  private generation = 0
  private readonly load: (from: IsoDate, to: IsoDate) => Promise<CalendarItem[]>

  constructor(load: (from: IsoDate, to: IsoDate) => Promise<CalendarItem[]>) {
    this.load = load
  }

  /** The month's items if already loaded — synchronously, so a revisit never flickers. */
  peek(month: IsoDate): CalendarItem[] | undefined {
    return this.loaded.get(month)
  }

  get(month: IsoDate): Promise<CalendarItem[]> {
    const hit = this.loaded.get(month)
    if (hit) return Promise.resolve(hit)

    const inFlight = this.pending.get(month)
    if (inFlight) return inFlight

    const generation = this.generation
    const { start, weeks } = monthGrid(month)
    const request = this.load(start, addDays(start, weeks * 7))
      .then((items) => {
        // A clear() while this was in flight means it may be stale — don't keep it.
        if (generation === this.generation) this.loaded.set(month, items)
        return items
      })
      .finally(() => this.pending.delete(month))
    this.pending.set(month, request)
    return request
  }

  /** Loads the month(s) next door in the background; failures are ignored (they'll load on visit). */
  prefetchAround(month: IsoDate, which: Prefetch): void {
    const offsets = which === 'both' ? [-1, 1] : which === 'prev' ? [-1] : which === 'next' ? [1] : []
    for (const m of offsets.map((n) => addMonths(month, n))) {
      if (!this.loaded.has(m) && !this.pending.has(m)) this.get(m).catch(() => {})
    }
  }

  clear(): void {
    this.generation++
    this.loaded.clear()
    this.pending.clear()
  }
}

type State =
  | { status: 'loading'; data: undefined; error: undefined }
  | { status: 'success'; data: CalendarItem[]; error: undefined }
  | { status: 'error'; data: undefined; error: Error }

/**
 * One month grid's items from `cache`, keeping the last month on screen while the next
 * one loads (`isRefreshing`). `version` bumps force a reload after `cache.clear()`.
 */
export function useMonthBookings(cache: MonthBookingsCache, month: IsoDate, version: number, prefetch: Prefetch = 'none') {
  const key = `${month}|${version}`
  const hit = cache.peek(month)
  const [shown, setShown] = useState<{ key: string; state: State }>(() => ({
    key,
    state: hit ? { status: 'success', data: hit, error: undefined } : { status: 'loading', data: undefined, error: undefined },
  }))

  // A new month (or a cleared cache), decided during render rather than in an effect:
  // already loaded → shown at once; otherwise the last month stays on screen while it loads.
  if (shown.key !== key) {
    setShown({ key, state: hit ? { status: 'success', data: hit, error: undefined } : shown.state })
  }

  useEffect(() => {
    let alive = true
    if (!hit) {
      cache
        .get(month)
        .then((data) => alive && setShown({ key, state: { status: 'success', data, error: undefined } }))
        .catch((error: unknown) => {
          if (alive) {
            setShown({ key, state: { status: 'error', data: undefined, error: error instanceof Error ? error : new Error(String(error)) } })
          }
        })
    }
    cache.prefetchAround(month, prefetch)
    return () => {
      alive = false
    }
  }, [cache, month, key, hit, prefetch])

  // A request for this month is in flight (or about to be) — unless it already failed.
  const isRefreshing = !hit && !(shown.key === key && shown.state.status === 'error')
  return { ...shown.state, isRefreshing }
}

/** A cache for this page's lifetime, plus a `refresh()` that empties it and reloads what's on screen. */
export function useMonthBookingsCache(token: string) {
  // The loader reads the token at call time, so a refreshed sign-in token is picked up
  // without throwing the cache away. Bookings become calendar items here, at the edge,
  // so nothing downstream knows what a booking is.
  const tokenRef = useRef(token)
  const [cache] = useState(
    // The loader runs later, per request — this is not a render-time read of the ref.
    // oxlint-disable-next-line react/refs
    () => new MonthBookingsCache((from, to) => getMyBookings(tokenRef.current, from, to).then((list) => list.map(bookingItem))),
  )
  useEffect(() => {
    tokenRef.current = token
  }, [token])

  const [version, setVersion] = useState(0)
  const refresh = useCallback(() => {
    cache.clear()
    setVersion((v) => v + 1)
  }, [cache])

  return { cache, version, refresh }
}
