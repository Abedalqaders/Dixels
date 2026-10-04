export type Theme = 'light' | 'dark'

// index.html reads this same key before React loads, so a saved theme is in place on the
// first paint instead of flashing light first. Change it in both places.
export const THEME_KEY = 'dixels.theme'

const listeners = new Set<() => void>()

/**
 * The theme the user picked with the toggle, or null if they never picked one — then the
 * app follows the computer's light/dark setting. Browser storage can be missing or blocked
 * (private mode), which also counts as "never picked".
 */
export function readSavedTheme(): Theme | null {
  try {
    const value = globalThis.localStorage?.getItem(THEME_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

/** Switches the whole app to `theme` and remembers it for next time. */
export function setTheme(theme: Theme): void {
  // tokens.css and the Tailwind `dark:` variant both key off this attribute.
  document.documentElement.dataset.theme = theme
  try {
    globalThis.localStorage?.setItem(THEME_KEY, theme)
  } catch {
    // Storage unavailable — the theme still changes, it just won't survive a reload.
  }
  listeners.forEach((listener) => listener())
}

/** For useSyncExternalStore: tells React when the saved theme changes. */
export function subscribeToTheme(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
