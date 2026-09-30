import { useTranslation } from 'react-i18next'
import { CheckIcon } from 'lucide-react'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { availableLanguages, currentLanguage, setLanguage } from '@/i18n'
import type { LanguageOption } from '@/i18n'
import { GlobeIcon } from './icons'

/**
 * Switches the app's language. The list is the backend's (ABP), so a language added there
 * shows up here without a frontend change.
 * - Two languages: one button that names the other one — quicker than a menu.
 * - More: a menu of every language.
 * Each name is written in its own language, so someone who can't read the current one
 * still finds theirs.
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t } = useTranslation()
  const languages = availableLanguages()
  const current = currentLanguage()

  if (languages.length < 2) return null

  if (languages.length === 2) {
    const other = languages.find((l) => l.code !== current) ?? languages[0]
    return (
      <button
        type="button"
        className={className}
        aria-label={`${t('Language:Label')}: ${other.name}`}
        onClick={() => void setLanguage(other.code)}
      >
        <GlobeIcon />
        <LanguageName language={other} />
      </button>
    )
  }

  const active = languages.find((l) => l.code === current) ?? languages[0]
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className={className} aria-label={`${t('Language:Label')}: ${active.name}`}>
          <GlobeIcon />
          <LanguageName language={active} />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        {languages.map((language) => (
          <DropdownMenuItem key={language.code} onSelect={() => void setLanguage(language.code)}>
            <CheckIcon className={language.code === current ? 'opacity-100' : 'opacity-0'} />
            <LanguageName language={language} />
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function LanguageName({ language }: { language: LanguageOption }) {
  return (
    <span lang={language.code} dir={language.dir}>
      {language.name}
    </span>
  )
}
