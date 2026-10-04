import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckIcon, CircleAlertIcon } from 'lucide-react'
import { FieldError } from '@/components/FieldError'
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

type LettersProblem = 'Dixels:Localization:NameHasForeignLetters' | 'Dixels:Localization:NameHasForeignLettersExceptCodes'

/** A short Latin code standing on its own: "IT", "B12", "3-01". */
const LATIN_CODE = /(?<![\p{L}\p{N}])[A-Z0-9][A-Z0-9-]{0,5}(?![\p{L}\p{N}])/gu
const LETTERS = /\p{L}/gu
const HAS_LETTER = /\p{L}/u

/** A regex for one letter of the alphabet `language` is written in, or null when the
 * browser can't say which (or has no such Unicode script, like Chinese "Hans"). */
function alphabetOf(language: string): { letter: RegExp; latin: boolean } | null {
  try {
    const script = new Intl.Locale(language).maximize().script
    if (!script) return null
    return { letter: new RegExp(String.raw`\p{scx=${script}}`, 'u'), latin: script === 'Latn' }
  } catch {
    return null
  }
}

/**
 * Whether `name` is in its language's own letters — Arabic in Arabic, English in English —
 * and if not, the message key saying so. Digits, spaces and punctuation are fine anywhere.
 * A language not written in Latin letters may still hold short Latin codes ("قسم IT",
 * "غرفة B12"), as long as it has a letter of its own. The same rule as NameAlphabet on
 * the backend: change the two together.
 */
export function nameLettersProblem(language: string, name: string): LettersProblem | null {
  const alphabet = alphabetOf(language)
  if (!alphabet) return null

  const letters = (alphabet.latin ? name : name.replace(LATIN_CODE, ' ')).match(LETTERS) ?? []
  // Codes and nothing else ("IT" as the Arabic name) is a name in another language.
  const fits = letters.every((c) => alphabet.letter.test(c)) && (letters.length > 0 || !HAS_LETTER.test(name))
  if (fits) return null
  return alphabet.latin ? 'Dixels:Localization:NameHasForeignLetters' : 'Dixels:Localization:NameHasForeignLettersExceptCodes'
}

/** The first language whose name has another language's letters — what a form switches
 * to (and refuses to save) — or undefined when every name is fine. */
export function languageWithForeignLetters(names: LocalizedNames): string | undefined {
  return availableLanguages()
    .map((l) => l.code)
    .find((code) => nameLettersProblem(code, names[code]?.trim() ?? ''))
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
 * the others are optional, so a missing one isn't flagged — only the default language's,
 * until it's filled in. The dropdown ticks the languages that have a name. The box types in the chosen language's
 * direction — Arabic right to left, even on an English screen.
 *
 * Each name must be in its own language's letters (nameLettersProblem): as soon as it isn't,
 * the box says so and the dropdown marks that language. The form refuses to save until it's
 * fixed — languageWithForeignLetters says which language to switch to.
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
  const problemOf = (code: string) => nameLettersProblem(code, value[code]?.trim() ?? '')
  const problem = problemOf(language)
  const problemId = `${id}-letters`
  const describedBy = [inputProps?.['aria-describedby'], problem && problemId].filter(Boolean).join(' ') || undefined

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
                  {problemOf(l.code) ? (
                    <CircleAlertIcon className="text-destructive" aria-label={t('Names:NeedsFixing')} />
                  ) : (
                    filled(l.code) && <CheckIcon className="text-brand" aria-label={t('Names:HasName')} />
                  )}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        <Input
          {...inputProps}
          aria-invalid={inputProps?.['aria-invalid'] || problem ? true : undefined}
          aria-describedby={describedBy}
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
      {problem && <FieldError id={problemId} message={t(problem, { language: current.name })} />}
      {languages.length > 1 && !filled(defaultLanguage) && (
        <p className="text-xs text-muted-foreground">
          {t('Names:DefaultRequired', { language: languageInfo(defaultLanguage).name })}
        </p>
      )}
    </div>
  )
}
