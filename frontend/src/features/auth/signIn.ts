import { readSavedTheme, type Theme } from '@/lib/theme'

/**
 * Extra parameters for every sign-in redirect. The backend's sign-in pages are on another
 * address, so they can't read the theme this app saved; `ui_theme` tells them which one is
 * showing here, and they open in it (backend Components/AccountTheme).
 */
export function signInExtras(): { extraQueryParams: Record<string, string> } {
  return { extraQueryParams: { ui_theme: themeShowing() } }
}

// The user's pick, else the computer's setting — what useTheme shows.
function themeShowing(): Theme {
  const saved = readSavedTheme()
  if (saved) return saved
  return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}
