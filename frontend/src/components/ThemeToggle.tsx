import { useTheme } from '@/hooks/useTheme'
import { MoonIcon, SunIcon } from './icons'

/** The light/dark switch. Shows where it will take you: a moon in light, a sun in dark. */
export function ThemeToggle({ className = '' }: { className?: string }) {
  const { theme, toggleTheme } = useTheme()
  const label = theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'

  return (
    <button type="button" className={`themebtn ${className}`.trim()} title={label} aria-label={label} onClick={toggleTheme}>
      {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
