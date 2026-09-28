import { useSyncExternalStore } from 'react'

/**
 * Whether a CSS media query matches right now, kept live as the window resizes. Where
 * matchMedia doesn't exist (tests, very old browsers) it reports `fallback`.
 */
export function useMediaQuery(query: string, fallback = true): boolean {
  return useSyncExternalStore(
    (onChange) => {
      if (typeof globalThis.matchMedia !== 'function') return () => {}
      const list = globalThis.matchMedia(query)
      list.addEventListener('change', onChange)
      return () => list.removeEventListener('change', onChange)
    },
    () => (typeof globalThis.matchMedia === 'function' ? globalThis.matchMedia(query).matches : fallback),
    () => fallback,
  )
}
