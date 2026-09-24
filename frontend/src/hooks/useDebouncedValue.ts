import { useEffect, useState } from 'react'

/** Delays reflecting `value` until it's stayed still for `delayMs` — used to hold off
 * refetching a paginated list on every keystroke, now that search costs a real server
 * round trip instead of client-side filtering. */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(id)
  }, [value, delayMs])

  return debounced
}
