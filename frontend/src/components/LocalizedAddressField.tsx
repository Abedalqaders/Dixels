import { useTranslation } from 'react-i18next'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { languageInfo } from '@/i18n'
import type { LocalizedNames } from '@/components/LocalizedNameField'

/** An address per language code, as the form holds it. Empty = no address. */
export type LocalizedAddresses = Record<string, string>

/**
 * The addresses as the API takes them: one per language that has both a name and an
 * address, trimmed. An address without a name would be dropped by the server anyway.
 */
export function toAddressList(names: LocalizedNames, addresses: LocalizedAddresses): { language: string; address: string }[] {
  return Object.entries(addresses)
    .map(([language, address]) => ({ language, address: address.trim() }))
    .filter((a) => a.address && names[a.language]?.trim())
}

/** The API's list back into the form's shape. */
export function fromAddressList(addresses: { language: string; address: string }[] | null | undefined): LocalizedAddresses {
  return Object.fromEntries((addresses ?? []).map((a) => [a.language, a.address]))
}

interface LocalizedAddressFieldProps {
  id: string
  value: LocalizedAddresses
  onChange: (value: LocalizedAddresses) => void
  /** The names beside it: a language needs a name before it can have an address. */
  names: LocalizedNames
  /** The language the name field above is showing — this box follows it. */
  language: string
  maxLength?: number
  disabled?: boolean
}

/**
 * An optional address in each of the app's languages, typed under a LocalizedNameField and
 * following its language dropdown: switch the name to Arabic and this box shows the Arabic
 * address. Its label names the language, a line under it says how to switch, and
 * `addressNote` marks the languages with an address in the name's dropdown. It stays
 * disabled until that language has a name.
 */
export function LocalizedAddressField({ id, value, onChange, names, language, maxLength, disabled }: LocalizedAddressFieldProps) {
  const { t } = useTranslation()
  const current = languageInfo(language)
  const named = Boolean(names[language]?.trim())
  const hintId = `${id}-hint`
  const helpId = `${id}-help`

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>
        {/* Which language this box is typing in — it follows the name's dropdown above. */}
        {t('Hierarchy:AddressIn', { language: current.name })}{' '}
        <span className="font-normal text-muted-foreground">{t('Hierarchy:Optional')}</span>
      </Label>
      <Input
        id={id}
        lang={current.code}
        dir={current.dir}
        value={value[language] ?? ''}
        onChange={(e) => onChange({ ...value, [language]: e.target.value })}
        placeholder={t('Hierarchy:AddressPlaceholder')}
        maxLength={maxLength}
        autoComplete="off"
        disabled={disabled || !named}
        aria-describedby={named ? helpId : `${hintId} ${helpId}`}
      />
      {!named && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {t('Hierarchy:AddressNeedsName', { language: current.name })}
        </p>
      )}
      <p id={helpId} className="text-xs text-muted-foreground">
        {t('Hierarchy:AddressOtherLanguages')}
      </p>
    </div>
  )
}

/** For LocalizedNameField's `languageNote`: marks the languages that have an address. */
export function addressNote(addresses: LocalizedAddresses, label: string) {
  return (code: string) => (addresses[code]?.trim() ? label : undefined)
}
