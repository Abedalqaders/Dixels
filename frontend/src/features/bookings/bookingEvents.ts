import { useEffect, useRef } from 'react'
import { getQueryClient } from '@/lib/api/queryClient'
import { queryKeys } from '@/lib/api/queryKeys'

// One signal for "my bookings changed" (made or cancelled). Every bookings query in the
// shared cache is invalidated — Find a space's day bars, the opened booking, the building —
// so whichever page is showing them refetches. My calendar's month cache keeps its own
// copy, so a window event still tells it to start over. Same-tab only; that's all the app needs.
const EVENT = 'dixels:bookings-changed'

export function emitBookingsChanged(): void {
  void getQueryClient()?.invalidateQueries({ queryKey: queryKeys.bookings.all })
  globalThis.dispatchEvent?.(new Event(EVENT))
}

/** Calls `onChange` whenever a booking is made or cancelled anywhere in the app. */
export function useBookingsChanged(onChange: () => void): void {
  const latest = useRef(onChange)
  useEffect(() => {
    latest.current = onChange
  })

  useEffect(() => {
    const handler = () => latest.current()
    globalThis.addEventListener?.(EVENT, handler)
    return () => globalThis.removeEventListener?.(EVENT, handler)
  }, [])
}
