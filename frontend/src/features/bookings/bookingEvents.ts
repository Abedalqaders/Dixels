import { useEffect, useRef } from 'react'

// One window event for "my bookings changed" (made or cancelled), so every page showing
// bookings — Find a space's day bars, My calendar — refreshes, whichever page the change
// came from. Same-tab only; that's all the app needs.
const EVENT = 'dixels:bookings-changed'

export function emitBookingsChanged(): void {
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
