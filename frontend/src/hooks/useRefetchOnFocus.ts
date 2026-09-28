import { useEffect, useRef } from 'react'

/**
 * Calls `refetch` when the user comes back to this tab. Data a page loaded once (a building's
 * rules, a day's bookings) can go stale while the tab sits in the background — an admin edits
 * a room's hours, a colleague books it — so re-reading it the moment someone looks again is
 * the cheap fix, without polling while nobody is watching.
 *
 * Uses visibilitychange rather than window focus: focus also fires on every click back from
 * devtools or an iframe, visibility only when the tab was actually hidden.
 */
export function useRefetchOnFocus(refetch: () => void): void {
  const latest = useRef(refetch)
  useEffect(() => {
    latest.current = refetch
  })

  useEffect(() => {
    const handler = () => {
      if (document.visibilityState === 'visible') latest.current()
    }
    document.addEventListener('visibilitychange', handler)
    return () => document.removeEventListener('visibilitychange', handler)
  }, [])
}
