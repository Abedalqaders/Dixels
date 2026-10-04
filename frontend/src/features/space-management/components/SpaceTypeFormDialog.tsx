import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useFieldErrors } from '@/components/FieldError'
import { TranslationsField, translationFieldName } from '@/components/TranslationsField'
import type { TranslationValue } from '@/components/TranslationsField'
import { getDefaultLanguage, languageInfo } from '@/i18n'
import { ApiError, createSpaceType, updateSpaceType } from '@/features/space-management/api/spaceManagementApi'
import type { IconKey, SpaceTypeDto, SpaceTypeNameDto } from '@/features/space-management/api/spaceManagementApi'
import { ICON_OPTIONS, ICONS, iconKeyToIconName } from './spaceTypeIcons'

/** SpaceTypeConsts.MaxNameLength on the backend. */
const MAX_NAME_LENGTH = 128

/** The error codes that are about one particular name, so they show under that name. */
const NAME_ERROR_CODES = new Set([
  'Dixels:SpaceManagement:SpaceTypeNameAlreadyExists',
  'Dixels:Localization:DefaultLanguageNameRequired',
])

interface SpaceTypeFormDialogProps {
  token: string
  /** The type being edited, or null to add a new one. */
  spaceType: SpaceTypeDto | null
  onClose: () => void
  onSaved: (message: string) => void
}

/** Every non-empty name, trimmed, as "language=name" — what "nothing changed" compares. */
function nameSet(names: SpaceTypeNameDto[]) {
  return names
    .map((n) => ({ language: n.language, name: n.name.trim() }))
    .filter((n) => n.name)
    .map((n) => `${n.language}=${n.name}`)
    .sort()
    .join('\n')
}

/** Add or edit one space type. Mounted only while open, so its fields always start from
 * the row that opened it.
 *
 * The name is one field in the default language (required: every language without its own
 * name shows it), then the translations the type has. Errors show where the admin is
 * typing: a duplicate under the very name that clashes (the server says which language),
 * anything else above the buttons. */
export function SpaceTypeFormDialog({ token, spaceType, onClose, onSaved }: SpaceTypeFormDialogProps) {
  const { t } = useTranslation()
  const defaultLanguage = getDefaultLanguage()
  const original = spaceType?.names ?? []

  const [name, setName] = useState(original.find((n) => n.language === defaultLanguage)?.name ?? '')
  const [translations, setTranslations] = useState<TranslationValue[]>(
    original.filter((n) => n.language !== defaultLanguage).map((n) => ({ language: n.language, name: n.name })),
  )
  const [iconKey, setIconKey] = useState(spaceType?.iconKey ?? 0)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const f = useFieldErrors<string>('st')

  const isEdit = spaceType !== null
  const trimmed = name.trim()
  const names: SpaceTypeNameDto[] = [
    { language: defaultLanguage, name: trimmed },
    // An added row left empty is no name at all — not sent.
    ...translations.map((tr) => ({ language: tr.language, name: tr.name.trim() })).filter((tr) => tr.name),
  ]
  const unchanged = isEdit && nameSet(names) === nameSet(original) && iconKey === spaceType.iconKey

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!trimmed || unchanged) return

    setSaving(true)
    setError(null)
    f.clear()
    try {
      if (isEdit) {
        await updateSpaceType(token, spaceType.id, { names, iconKey })
        onSaved(t('SpaceTypes:Updated'))
      } else {
        await createSpaceType(token, { names, iconKey })
        onSaved(t('SpaceTypes:Added'))
      }
    } catch (err) {
      if (err instanceof ApiError && NAME_ERROR_CODES.has(err.code ?? '')) {
        const language = err.data?.language
        const onRow = typeof language === 'string' && translations.some((tr) => tr.language === language)
        f.setErrors({ [onRow ? translationFieldName(language) : 'name']: err.message })
      } else {
        setError(err instanceof ApiError ? err.message : t('Error:Generic'))
      }
      setSaving(false)
    }
  }

  const defaultLanguageInfo = languageInfo(defaultLanguage)

  return (
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form {...f.form} onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle>{isEdit ? t('SpaceTypes:EditTitle', { name: spaceType.name }) : t('SpaceTypes:AddTitle')}</DialogTitle>
            <DialogDescription>{t('SpaceTypes:FormDetail')}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor={f.id('name')}>{t('SpaceTypes:NameIn', { language: defaultLanguageInfo.name })}</Label>
            <Input
              {...f.field('name')}
              lang={defaultLanguage}
              dir={defaultLanguageInfo.dir}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('SpaceTypes:NamePlaceholder')}
              maxLength={MAX_NAME_LENGTH}
              required
              autoComplete="off"
              autoFocus
              disabled={saving}
            />
            {f.error('name')}
          </div>

          <TranslationsField
            value={translations}
            onChange={setTranslations}
            fieldErrors={f}
            maxLength={MAX_NAME_LENGTH}
            disabled={saving}
          />

          <div className="flex flex-col gap-2">
            <Label id="st-icon-label">{t('SpaceTypes:Icon')}</Label>
            <ToggleGroup
              type="single"
              variant="outline"
              aria-labelledby="st-icon-label"
              value={String(iconKey)}
              // Radix sends '' when the selected item is clicked again — keep the current icon.
              onValueChange={(v) => v && setIconKey(Number(v) as IconKey)}
              disabled={saving}
            >
              {ICON_OPTIONS.map((o) => (
                <ToggleGroupItem key={o.value} value={String(o.value)} aria-label={t(o.labelKey)} title={t(o.labelKey)}>
                  {ICONS[iconKeyToIconName(o.value)]}
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
          </div>

          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
              {t('Common:Cancel')}
            </Button>
            <Button type="submit" disabled={saving || !trimmed || unchanged}>
              {saving ? t('Common:Saving') : isEdit ? t('Common:Save') : t('SpaceTypes:Add')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
