import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useFieldErrors } from '@/components/FieldError'
import { LocalizedNameField, languageWithForeignLetters, toNameList } from '@/components/LocalizedNameField'
import type { LocalizedNames } from '@/components/LocalizedNameField'
import { LocalizedAddressField, addressNote, toAddressList } from '@/components/LocalizedAddressField'
import type { LocalizedAddresses } from '@/components/LocalizedAddressField'
import { getDefaultLanguage } from '@/i18n'
import { TimezonePicker } from '@/components/TimezonePicker'
import {
  ApiError,
  createBuilding,
  createFloor,
  createSpace,
} from '@/features/space-management/api/spaceManagementApi'
import type { OperatingWindowDto, SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'

export type ModalState =
  | { kind: 'building' }
  | { kind: 'floor'; parentName: string; parentId: string }
  | { kind: 'space'; parentName: string; parentId: string }
  | null

// A newly-created building has no constraints editor input in this quick-add form (that's
// what the dedicated constraints page is for) — it starts maximally permissive, and the
// admin narrows it down afterward.
const DEFAULT_BUILDING_DAYS = [0, 1, 2, 3, 4, 5, 6]
const DEFAULT_BUILDING_HOURS: OperatingWindowDto = { isOpen24Hours: true, open: '00:00', close: '00:00' }
const DEFAULT_BUILDING_MAX_DURATION_MINUTES = 120
const DEFAULT_BUILDING_MAX_HORIZON_DAYS = 30
const DEFAULT_BUILDING_MIN_LEAD_MINUTES = 0

/** LocalizedNameConsts.MaxNameLength on the backend. */
const MAX_NAME_LENGTH = 128
/** BuildingConsts.MaxAddressLength on the backend. */
const MAX_ADDRESS_LENGTH = 512

interface AddNodeModalProps {
  state: NonNullable<ModalState>
  token: string
  spaceTypes: SpaceTypeDto[]
  onClose: () => void
  onCreated: () => void
  onError: (message: string) => void
}

type Field = 'name' | 'meta' | 'type'

export function AddNodeModal({ state, token, spaceTypes, onClose, onCreated, onError }: AddNodeModalProps) {
  const { t } = useTranslation()
  // A name per language, typed in one box (LocalizedNameField); the default language's is required.
  const defaultLanguage = getDefaultLanguage()
  const [names, setNames] = useState<LocalizedNames>({})
  // A building's address, per language like its name; optional.
  const [addresses, setAddresses] = useState<LocalizedAddresses>({})
  const [language, setLanguage] = useState(defaultLanguage)
  const [meta, setMeta] = useState('')
  const [timezone, setTimezone] = useState('UTC')
  const [spaceTypeId, setSpaceTypeId] = useState(spaceTypes[0]?.id ?? '')
  const [submitting, setSubmitting] = useState(false)
  const f = useFieldErrors<Field>('add')

  const titles = { building: t('Hierarchy:AddBuilding'), floor: t('Hierarchy:AddFloor'), space: t('Hierarchy:AddSpace') }
  const metaLabel =
    state.kind === 'building' ? t('Hierarchy:BuildingNumber') : state.kind === 'floor' ? t('Hierarchy:FloorNumber') : t('Hierarchy:Capacity')
  const metaPlaceholder =
    state.kind === 'building'
      ? t('Hierarchy:BuildingNumberPlaceholder')
      : state.kind === 'floor'
        ? t('Hierarchy:FloorNumberPlaceholder')
        : t('Hierarchy:CapacityPlaceholder')
  const metaRequired = state.kind === 'space' // Building/Floor number are optional; Capacity is required.

  // Every problem at once, each under its own field — not the first one found.
  function validate(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {}
    if (!names[defaultLanguage]?.trim()) errors.name = t('Hierarchy:NameRequired')
    if (state.kind === 'floor' && meta.trim() && !Number.isInteger(Number(meta))) {
      errors.meta = t('Hierarchy:FloorNumberWhole')
    }
    if (state.kind === 'space') {
      const capacity = Number(meta)
      if (!meta.trim() || !Number.isFinite(capacity) || capacity <= 0) errors.meta = t('Hierarchy:CapacityPositive')
      if (!spaceTypeId) errors.type = t('Hierarchy:ChooseSpaceType')
    }
    return errors
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const errors = validate()
    // A name in another language's letters: the box says so itself, once it shows that language.
    const misspelt = languageWithForeignLetters(names)
    if (Object.keys(errors).length > 0 || misspelt) {
      // The missing name is the default language's: show that one.
      if (errors.name) setLanguage(defaultLanguage)
      else if (misspelt) setLanguage(misspelt)
      f.setErrors(errors)
      return
    }
    f.clear()

    setSubmitting(true)
    try {
      if (state.kind === 'building') {
        await createBuilding(token, {
          names: toNameList(names),
          addresses: toAddressList(names, addresses),
          buildingNumber: meta.trim() || null,
          timezone,
          days: DEFAULT_BUILDING_DAYS,
          hours: DEFAULT_BUILDING_HOURS,
          maxDurationMinutes: DEFAULT_BUILDING_MAX_DURATION_MINUTES,
          maxHorizonDays: DEFAULT_BUILDING_MAX_HORIZON_DAYS,
          minLeadMinutes: DEFAULT_BUILDING_MIN_LEAD_MINUTES,
        })
      } else if (state.kind === 'floor') {
        await createFloor(token, {
          buildingId: state.parentId,
          names: toNameList(names),
          floorNumber: meta.trim() ? Number(meta) : null,
        })
      } else {
        await createSpace(token, { floorId: state.parentId, names: toNameList(names), spaceTypeId, capacity: Number(meta) })
      }

      onCreated()
      onClose()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : t('Error:Generic'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form {...f.form} onSubmit={handleSubmit} noValidate>
          <DialogHeader>
            <DialogTitle>{titles[state.kind]}</DialogTitle>
            {state.kind !== 'building' && <DialogDescription>{t('Hierarchy:AddedUnder', { name: state.parentName })}</DialogDescription>}
          </DialogHeader>

          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <LocalizedNameField
                id={f.id('name')}
                label={
                  <>
                    {t('Hierarchy:Name')}<span className="text-destructive">*</span>
                  </>
                }
                value={names}
                onChange={setNames}
                language={language}
                onLanguageChange={setLanguage}
                inputProps={f.field('name')}
                error={f.error('name')}
                placeholder={state.kind === 'floor' ? t('Hierarchy:FloorNamePlaceholder') : t('Hierarchy:Name')}
                maxLength={MAX_NAME_LENGTH}
                autoFocus
                languageNote={state.kind === 'building' ? addressNote(addresses, t('Hierarchy:HasAddress')) : undefined}
              />
            </div>
            {state.kind === 'building' && (
              <div className="sm:col-span-2">
                <LocalizedAddressField
                  id="add-address"
                  value={addresses}
                  onChange={setAddresses}
                  names={names}
                  language={language}
                  maxLength={MAX_ADDRESS_LENGTH}
                />
              </div>
            )}
            <div className="grid gap-2">
              <Label htmlFor={f.id('meta')}>
                {metaLabel}
                {metaRequired && <span className="text-destructive">*</span>}
              </Label>
              <Input
                {...f.field('meta')}
                // A building number is free text (it can be Arabic, which the mono font breaks
                // apart); a floor number or capacity is a number.
                className={state.kind === 'building' ? 'tabular-nums' : 'font-mono'}
                placeholder={metaPlaceholder}
                value={meta}
                onChange={(e) => setMeta(e.target.value)}
              />
              {f.error('meta')}
            </div>

            {state.kind === 'building' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor="add-timezone">
                  {t('Timezone:Label')}<span className="text-destructive">*</span>
                </Label>
                <TimezonePicker id="add-timezone" value={timezone} onChange={setTimezone} />
              </div>
            )}

            {state.kind === 'space' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor={f.id('type')}>
                  {t('Hierarchy:Type')}<span className="text-destructive">*</span>
                </Label>
                <Select value={spaceTypeId} onValueChange={setSpaceTypeId}>
                  <SelectTrigger {...f.field('type')} className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {spaceTypes.map((st) => (
                      <SelectItem key={st.id} value={st.id}>
                        {st.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {f.error('type')}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={submitting}>
              {t('Common:Cancel')}
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? t('Hierarchy:Adding') : t('Hierarchy:Add')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
