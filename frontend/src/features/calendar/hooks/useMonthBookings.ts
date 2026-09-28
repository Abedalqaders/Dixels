import { useCallback, useEffect, useRef, useState } from 'react'
import { addDays } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { getMyBookings } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { addMonths, monthGrid } from '@/features/calendar/calendarDates'

/**
 * My bookings, one month grid at a time (the Sunday before the 1st to the Saturday after
 * the last day, ≤ 42 days — one request, however the page is looking at it). Loaded months
 * stay in memory, and the months either side are fetched in the background, so stepping
 * back and forth shows data straight away. `clear()` drops everything, for after a booking
 * is made or cancelled.
 */
export class MonthBookingsCache {
  private readonly loaded = new Map<IsoDate, BookingDto[]>()
  private readonly pending = new Map<IsoDate, Promise<BookingDto[]>>()
  private generation = 0
  private readonly load: (from: IsoDate, to: IsoDate) => Promise<BookingDto[]>

  constructor(load: (from: IsoDate, to: IsoDate) => Promise<BookingDto[]>) {
    this.load = load
  }

  /** The month's bookings if already loaded — synchronously, so a revisit never flickers. */
  peek(month: IsoDate): BookingDto[] | undefined {
    return this.loaded.get(month)
  }

  get(month: IsoDate): Promise<BookingDto[]> {
    const hit = this.loaded.get(month)
    if (hit) return Promise.resolve(hit)

    const inFlight = this.pending.get(month)
    if (inFlight) return inFlight

    const generation = this.generation
    const { start, weeks } = monthGrid(month)
    const request = this.load(start, addDays(start, weeks * 7))
      .then((bookings) => {
        // A clear() while this was in flight means it may be stale — don't keep it.
        if (generation === this.generation) this.loaded.set(month, bookings)
        return bookings
      })
      .finally(() => this.pending.delete(month))
    this.pending.set(month, request)
    return request
  }

  /** Loads the months either side in the background; failures are ignored (they'll load on visit). */
  prefetchAround(month: IsoDate): void {
    for (const m of [addMonths(month, -1), addMonths(month, 1)]) {
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
  | { status: 'success'; data: BookingDto[]; error: undefined }
  | { status: 'error'; data: undefined; error: Error }

/**
 * One month grid's bookings from `cache`, keeping the last month on screen while the next
 * one loads (`isRefreshing`). `version` bumps force a reload after `cache.clear()`.
 */
export function useMonthBookings(cache: MonthBookingsCache, month: IsoDate, version: number, prefetch = false) {
  const initial = cache.peek(month)
  const [state, setState] = useState<State>(
    initial ? { status: 'success', data: initial, error: undefined } : { status: 'loading', data: undefined, error: undefined },
  )
  const [isRefreshing, setRefreshing] = useState(!initial)

  useEffect(() => {
    let alive = true
    const hit = cache.peek(month)
    if (hit) {
      setState({ status: 'success', data: hit, error: undefined })
      setRefreshing(false)
    } else {
      setRefreshing(true)
      cache
        .get(month)
        .then((data) => alive && setState({ status: 'success', data, error: undefined }))
        .catch((error: unknown) => {
          if (alive) setState({ status: 'error', data: undefined, error: error instanceof Error ? error : new Error(String(error)) })
        })
        .finally(() => alive && setRefreshing(false))
    }
    if (prefetch) cache.prefetchAround(month)
    return () => {
      alive = false
    }
  }, [cache, month, version, prefetch])

  return { ...state, isRefreshing }
}

/** A cache for this page's lifetime, plus a `refresh()` that empties it and reloads what's on screen. */
export function useMonthBookingsCache(token: string) {
  // The loader reads the token at call time, so a refreshed sign-in token is picked up
  // without throwing the cache away.
  const tokenRef = useRef(token)
  const [cache] = useState(() => new MonthBookingsCache((from, to) => getMyBookings(tokenRef.current, from, to)))
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
