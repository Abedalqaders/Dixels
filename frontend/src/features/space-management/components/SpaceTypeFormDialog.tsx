import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useFieldErrors } from '@/components/FieldError'
import { LocalizedNameField, fromNameList, toNameList } from '@/components/LocalizedNameField'
import type { LocalizedNames } from '@/components/LocalizedNameField'
import { currentLanguage, getDefaultLanguage } from '@/i18n'
import { ApiError, createSpaceType, updateSpaceType } from '@/features/space-management/api/spaceManagementApi'
import type { IconKey, LocalizedNameDto, SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { ICON_OPTIONS, ICONS, iconKeyToIconName } from './spaceTypeIcons'

/** SpaceTypeConsts.MaxNameLength on the backend. */
const MAX_NAME_LENGTH = 128

/** The error codes that are about one particular name, so they show under the name field. */
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
function nameSet(names: LocalizedNameDto[]) {
  return toNameList(fromNameList(names))
    .map((n) => `${n.language}=${n.name}`)
    .sort()
    .join('\n')
}

/** Add or edit one space type. Mounted only while open, so its fields always start from
 * the row that opened it.
 *
 * The name is one box with a language beside it (LocalizedNameField): it opens on the
 * language the admin is using, and the default language's name is required. Errors show
 * where the admin is typing — a duplicate switches the box to the language that clashes
 * (the server says which) — anything else above the buttons. */
export function SpaceTypeFormDialog({ token, spaceType, onClose, onSaved }: SpaceTypeFormDialogProps) {
  const { t } = useTranslation()
  const defaultLanguage = getDefaultLanguage()
  const original = spaceType?.names ?? []

  const [names, setNames] = useState<LocalizedNames>(() => fromNameList(original))
  // A new type starts in the default language (its name is the one required); an existing
  // one in the admin's own language.
  const [language, setLanguage] = useState(spaceType ? currentLanguage() : defaultLanguage)
  const [iconKey, setIconKey] = useState(spaceType?.iconKey ?? 0)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const f = useFieldErrors<'name'>('st')

  const isEdit = spaceType !== null
  const list = toNameList(names)
  const hasDefault = list.some((n) => n.language === defaultLanguage)
  const unchanged = isEdit && nameSet(list) === nameSet(original) && iconKey === spaceType.iconKey

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!hasDefault || unchanged) return

    setSaving(true)
    setError(null)
    f.clear()
    try {
      if (isEdit) {
        await updateSpaceType(token, spaceType.id, { names: list, iconKey })
        onSaved(t('SpaceTypes:Updated'))
      } else {
        await createSpaceType(token, { names: list, iconKey })
        onSaved(t('SpaceTypes:Added'))
      }
    } catch (err) {
      if (err instanceof ApiError && NAME_ERROR_CODES.has(err.code ?? '')) {
        const clash = err.data?.language
        if (typeof clash === 'string' && clash in names) setLanguage(clash)
        f.setErrors({ name: err.message })
      } else {
        setError(err instanceof ApiError ? err.message : t('Error:Generic'))
      }
      setSaving(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form {...f.form} onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle>{isEdit ? t('SpaceTypes:EditTitle', { name: spaceType.name }) : t('SpaceTypes:AddTitle')}</DialogTitle>
            <DialogDescription>{t('SpaceTypes:FormDetail')}</DialogDescription>
          </DialogHeader>

          <LocalizedNameField
            id={f.id('name')}
            label={t('SpaceTypes:ColumnName')}
            value={names}
            onChange={setNames}
            language={language}
            onLanguageChange={setLanguage}
            inputProps={f.field('name')}
            error={f.error('name')}
            placeholder={t('SpaceTypes:NamePlaceholder')}
            maxLength={MAX_NAME_LENGTH}
            disabled={saving}
            autoFocus
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
            <Button type="submit" disabled={saving || !hasDefault || unchanged}>
              {saving ? t('Common:Saving') : isEdit ? t('Common:Save') : t('SpaceTypes:Add')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
