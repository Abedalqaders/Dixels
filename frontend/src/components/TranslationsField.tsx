import { useId, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronDownIcon, PlusIcon, XIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { useFieldErrors } from '@/components/FieldError'
import { availableLanguages, getDefaultLanguage, languageInfo } from '@/i18n'

/** A name in one language, as the form holds it (possibly still empty while being typed). */
export interface TranslationValue {
  language: string
  name: string
}

/** The field name a translation's error is filed under in useFieldErrors: "name-ar". */
export function translationFieldName(language: string) {
  return `name-${language}`
}

interface TranslationsFieldProps {
  /** Names in languages other than the default, in the order they were added. */
  value: TranslationValue[]
  onChange: (value: TranslationValue[]) => void
  /** The form's useFieldErrors: each row's input gets its id and error from it. */
  fieldErrors: ReturnType<typeof useFieldErrors<string>>
  maxLength?: number
  disabled?: boolean
}

/**
 * The names in every language besides the default one, for any entity whose name people
 * type (space types now; buildings, floors and spaces next). Only the languages added are
 * shown — with ten languages, ten empty boxes would hide the one that matters — and
 * "Add translation" offers the rest. The languages come from the backend (ABP), so a new
 * one shows up here with no frontend change.
 *
 * Each row's label is the language's own name in its own script and direction, and its
 * input is in that direction too: an Arabic name is typed right to left even on an English
 * screen.
 */
export function TranslationsField({ value, onChange, fieldErrors, maxLength, disabled }: TranslationsFieldProps) {
  const { t } = useTranslation()
  const defaultLanguage = getDefaultLanguage()
  const others = availableLanguages().filter((l) => l.code !== defaultLanguage)
  const missing = others.filter((l) => !value.some((v) => v.language === l.code))

  const titleId = useId()
  const inputs = useRef(new Map<string, HTMLInputElement>())
  // The row just added gets focus once the menu has closed (the menu would otherwise hand
  // it back to its own button), so the admin can type straight away.
  const focusAfterClose = useRef<string | null>(null)

  if (others.length === 0) return null

  function update(language: string, name: string) {
    onChange(value.map((v) => (v.language === language ? { ...v, name } : v)))
  }

  function remove(language: string) {
    onChange(value.filter((v) => v.language !== language))
  }

  function add(language: string) {
    onChange([...value, { language, name: '' }])
    focusAfterClose.current = language
  }

  return (
    <div role="group" aria-labelledby={titleId} className="flex flex-col gap-3">
      <div className="flex items-baseline justify-between gap-2">
        <span id={titleId} className="text-sm font-medium">
          {t('Translations:Title')}
        </span>
        <span className="text-xs text-muted-foreground">
          {t('Translations:Count', { count: value.length, total: others.length })}
        </span>
      </div>
      <p className="-mt-2 text-xs text-muted-foreground">
        {t('Translations:Hint', { language: languageInfo(defaultLanguage).name })}
      </p>

      {value.map((v) => {
        const language = languageInfo(v.language)
        const field = translationFieldName(v.language)
        return (
          <div key={v.language} className="flex flex-col gap-1">
            <Label htmlFor={fieldErrors.id(field)}>
              <span lang={language.code} dir={language.dir}>
                {language.name}
              </span>
            </Label>
            <div className="flex items-center gap-2">
              <Input
                {...fieldErrors.field(field)}
                ref={(el) => {
                  if (el) inputs.current.set(v.language, el)
                  else inputs.current.delete(v.language)
                }}
                lang={language.code}
                dir={language.dir}
                value={v.name}
                onChange={(e) => update(v.language, e.target.value)}
                maxLength={maxLength}
                autoComplete="off"
                disabled={disabled}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={t('Translations:Remove', { language: language.name })}
                title={t('Translations:Remove', { language: language.name })}
                onClick={() => remove(v.language)}
                disabled={disabled}
              >
                <XIcon />
              </Button>
            </div>
            {fieldErrors.error(field)}
          </div>
        )
      })}

      {missing.length > 0 && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button type="button" variant="outline" size="sm" className="self-start" disabled={disabled}>
              <PlusIcon />
              {t('Translations:Add')}
              <ChevronDownIcon />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            align="start"
            onCloseAutoFocus={(e) => {
              const language = focusAfterClose.current
              if (!language) return
              e.preventDefault()
              focusAfterClose.current = null
              inputs.current.get(language)?.focus()
            }}
          >
            {missing.map((l) => (
              <DropdownMenuItem key={l.code} onSelect={() => add(l.code)}>
                <span lang={l.code} dir={l.dir}>
                  {l.name}
                </span>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}
