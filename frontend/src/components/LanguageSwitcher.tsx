import { useTranslation } from 'react-i18next'
import { ChevronDownIcon, CircleCheckIcon } from 'lucide-react'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { availableLanguages, currentLanguage, languageInfo, setLanguage } from '@/i18n'
import type { LanguageOption } from '@/i18n'
import { cn } from '@/lib/utils'
import { GlobeIcon } from './icons'

/**
 * Switches the app's language: a button naming the current one, opening a list of every
 * language. The list is the backend's (ABP), so a language added there shows up here without
 * a frontend change.
 *
 * Each name is written in its own language ("العربية"), so someone who can't read the current
 * one still finds theirs; next to it, in grey, the same name in the language showing now
 * ("Arabic"), so it's clear what each one is.
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t } = useTranslation()
  const languages = availableLanguages()
  const current = currentLanguage()

  if (languages.length < 2) return null

  const active = languages.find((l) => l.code === current) ?? languages[0]
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className={cn('langtrigger', className)} aria-label={`${t('Language:Label')}: ${active.name}`}>
          <GlobeIcon />
          <LanguageLabel language={active} viewer={current} />
          <ChevronDownIcon className="langchev" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="min-w-64 overflow-hidden rounded-xl p-0 shadow-lg">
        {languages.map((language) => {
          const selected = language.code === current
          return (
            <DropdownMenuItem
              key={language.code}
              role="menuitemradio"
              aria-checked={selected}
              className={cn(
                'gap-3 rounded-none border-b border-border px-4 py-3 text-[15px] last:border-b-0 focus:bg-muted focus:text-foreground',
                selected && 'bg-muted',
              )}
              onSelect={() => void setLanguage(language.code)}
            >
              <LanguageLabel language={language} viewer={current} />
              {selected && <CircleCheckIcon className="ms-auto" aria-hidden />}
            </DropdownMenuItem>
          )
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/** "العربية  Arabic" — the grey part left out when it would only repeat the name. */
function LanguageLabel({ language, viewer }: { language: LanguageOption; viewer: string }) {
  const translated = viewer === language.code ? undefined : nameIn(viewer, language.code)
  return (
    <span className="langlabel">
      <span lang={language.code} dir={language.dir}>
        {language.name}
      </span>
      {translated && translated.toLocaleLowerCase() !== language.name.toLocaleLowerCase() && (
        <span className="langother" lang={viewer} dir={languageInfo(viewer).dir}>
          {translated}
        </span>
      )}
    </span>
  )
}

/** A language's name in another language — "Arabic" in English — or undefined if Intl can't say. */
function nameIn(viewer: string, code: string): string | undefined {
  try {
    const name = new Intl.DisplayNames([viewer], { type: 'language' }).of(code)
    return name && name !== code ? name : undefined
  } catch {
    return undefined
  }
}
