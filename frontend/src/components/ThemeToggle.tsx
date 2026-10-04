import { useTranslation } from 'react-i18next'
import { useTheme } from '@/hooks/useTheme'
import { MoonIcon, SunIcon } from './icons'

/** The light/dark switch. Shows where it will take you: a moon in light, a sun in dark. */
export function ThemeToggle({ className = '' }: { className?: string }) {
  const { t } = useTranslation()
  const { theme, toggleTheme } = useTheme()
  const label = theme === 'dark' ? t('Theme:ToLight') : t('Theme:ToDark')

  return (
    <button type="button" className={`themebtn ${className}`.trim()} title={label} aria-label={label} onClick={toggleTheme}>
      {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
