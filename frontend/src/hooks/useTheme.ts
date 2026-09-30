import { useSyncExternalStore } from 'react'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { readSavedTheme, setTheme, subscribeToTheme, type Theme } from '@/lib/theme'

/**
 * The theme on screen right now, and a way to flip it. Until the user picks one it is
 * whatever the computer is set to; after that their pick wins.
 */
export function useTheme(): { theme: Theme; toggleTheme: () => void } {
  const saved = useSyncExternalStore(subscribeToTheme, readSavedTheme, () => null)
  const systemDark = useMediaQuery('(prefers-color-scheme: dark)', false)
  const theme: Theme = saved ?? (systemDark ? 'dark' : 'light')

  return { theme, toggleTheme: () => setTheme(theme === 'dark' ? 'light' : 'dark') }
}
