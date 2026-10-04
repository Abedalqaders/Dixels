import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckIcon } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { availableLanguages, getDefaultLanguage, languageInfo } from '@/i18n'

/** A name per language code, as the form holds it: { en: 'Desk', ar: 'مكتب' }. Empty = no name. */
export type LocalizedNames = Record<string, string>

/** The names as the API takes them: one entry per language with a name, trimmed. */
export function toNameList(names: LocalizedNames): { language: string; name: string }[] {
  return Object.entries(names)
    .map(([language, name]) => ({ language, name: name.trim() }))
    .filter((n) => n.name)
}

/** The API's list back into the form's shape. */
export function fromNameList(names: { language: string; name: string }[]): LocalizedNames {
  return Object.fromEntries(names.map((n) => [n.language, n.name]))
}

interface LocalizedNameFieldProps {
  /** The field's id, its label's target — and the error's, through `inputProps`. */
  id: string
  label: ReactNode
  value: LocalizedNames
  onChange: (value: LocalizedNames) => void
  /** Which language the box is showing. Controlled, so the form can jump to the language a
   * server error is about. */
  language: string
  onLanguageChange: (language: string) => void
  /** Spread onto the input (aria-invalid, aria-describedby from useFieldErrors). */
  inputProps?: Record<string, unknown>
  /** The message under the field, if any. */
  error?: ReactNode
  placeholder?: string
  maxLength?: number
  disabled?: boolean
  autoFocus?: boolean
}

/**
 * A name typed in any of the app's languages, with one text box: the dropdown beside it
 * picks the language, and the box shows and edits the name in that language. The
 * languages come from the backend (ABP), so a new one appears here with no frontend change.
 *
 * The default language's name is required (every language without its own name shows it);
 * the others are optional. The dropdown ticks the languages that have a name, and the line
 * under the box says which are still missing. The box types in the chosen language's
 * direction — Arabic right to left, even on an English screen.
 */
export function LocalizedNameField({
  id,
  label,
  value,
  onChange,
  language,
  onLanguageChange,
  inputProps,
  error,
  placeholder,
  maxLength,
  disabled,
  autoFocus,
}: LocalizedNameFieldProps) {
  const { t } = useTranslation()
  const languages = availableLanguages()
  const defaultLanguage = getDefaultLanguage()
  const current = languageInfo(language)
  const filled = (code: string) => Boolean(value[code]?.trim())
  const missing = languages.filter((l) => !filled(l.code))

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        {languages.length > 1 && (
          <Select value={language} onValueChange={onLanguageChange} disabled={disabled}>
            <SelectTrigger className="w-36 flex-none" aria-label={t('Names:Language')}>
              <SelectValue>
                <span lang={current.code} dir={current.dir}>
                  {current.name}
                </span>
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {languages.map((l) => (
                <SelectItem key={l.code} value={l.code}>
                  <span lang={l.code} dir={l.dir}>
                    {l.name}
                  </span>
                  {l.code === defaultLanguage && <span className="text-xs text-muted-foreground">{t('Names:Required')}</span>}
                  {filled(l.code) && <CheckIcon className="text-brand" aria-label={t('Names:HasName')} />}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        <Input
          {...inputProps}
          id={id}
          lang={current.code}
          dir={current.dir}
          value={value[language] ?? ''}
          onChange={(e) => onChange({ ...value, [language]: e.target.value })}
          placeholder={placeholder}
          maxLength={maxLength}
          autoComplete="off"
          autoFocus={autoFocus}
          disabled={disabled}
        />
      </div>
      {error}
      {languages.length > 1 && missing.length > 0 && (
        <p className="text-xs text-muted-foreground">
          {filled(defaultLanguage)
            ? t('Translations:Missing', { languages: missing.map((l) => l.name).join(', ') })
            : t('Names:DefaultRequired', { language: languageInfo(defaultLanguage).name })}
        </p>
      )}
    </div>
  )
}
