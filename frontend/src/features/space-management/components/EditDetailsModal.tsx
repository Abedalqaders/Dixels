import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useFieldErrors } from '@/components/FieldError'
import { LocalizedNameField, languageWithForeignLetters, fromNameList, toNameList } from '@/components/LocalizedNameField'
import type { LocalizedNames } from '@/components/LocalizedNameField'
import { LocalizedAddressField, addressNote, fromAddressList, toAddressList } from '@/components/LocalizedAddressField'
import type { LocalizedAddresses } from '@/components/LocalizedAddressField'
import { currentLanguage, getDefaultLanguage } from '@/i18n'
import { TimezonePicker } from '@/components/TimezonePicker'
import { ApiError, updateBuilding, updateFloor, updateSpace, getSpaceUpdateImpact } from '@/features/space-management/api/spaceManagementApi'
import type { BuildingAddressDto, LocalizedNameDto, SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'

// The identity-fields counterpart to AddNodeModal — Name/BuildingNumber/Timezone (Building),
// Name/FloorNumber (Floor), Name/SpaceType/Capacity (Space). Deliberately separate from the
// constraints page: these hit updateBuilding/updateFloor/updateSpace, not the constraints
// endpoints, and none of the three identity DTOs carry a ConcurrencyStamp — the backend does
// a plain overwrite here, unlike the constraints save.
//
// Same shadcn Dialog as AddNodeModal: focus stays inside, Escape and the overlay close it,
// and the title is announced — none of which the old hand-rolled overlay did.
// `names` is every name the row has, one per language (its DTO's `names`).
export type EditDetailsState =
  | { kind: 'building'; id: string; names: LocalizedNameDto[]; addresses: BuildingAddressDto[]; buildingNumber: string | null; timezone: string }
  | { kind: 'floor'; id: string; names: LocalizedNameDto[]; floorNumber: number | null }
  | { kind: 'space'; id: string; names: LocalizedNameDto[]; spaceTypeId: string; capacity: number }
  | null

/** LocalizedNameConsts.MaxNameLength on the backend. */
const MAX_NAME_LENGTH = 128
/** BuildingConsts.MaxAddressLength on the backend. */
const MAX_ADDRESS_LENGTH = 512

interface EditDetailsModalProps {
  state: NonNullable<EditDetailsState>
  token: string
  spaceTypes: SpaceTypeDto[]
  onClose: () => void
  onSaved: () => void
  onError: (message: string) => void
}

type Field = 'name' | 'floorNumber' | 'capacity' | 'type'

export function EditDetailsModal({ state, token, spaceTypes, onClose, onSaved, onError }: EditDetailsModalProps) {
  const { t } = useTranslation()
  // A name per language, typed in one box (LocalizedNameField) that opens on the admin's own
  // language; the default language's is required.
  const defaultLanguage = getDefaultLanguage()
  const [names, setNames] = useState<LocalizedNames>(() => fromNameList(state.names))
  // A building's address, per language like its name; optional.
  const [addresses, setAddresses] = useState<LocalizedAddresses>(() => (state.kind === 'building' ? fromAddressList(state.addresses) : {}))
  const [language, setLanguage] = useState(currentLanguage())
  const [buildingNumber, setBuildingNumber] = useState(state.kind === 'building' ? (state.buildingNumber ?? '') : '')
  const [timezone, setTimezone] = useState(state.kind === 'building' ? state.timezone : 'UTC')
  const [floorNumber, setFloorNumber] = useState(state.kind === 'floor' ? String(state.floorNumber ?? '') : '')
  const [spaceTypeId, setSpaceTypeId] = useState(state.kind === 'space' ? state.spaceTypeId : (spaceTypes[0]?.id ?? ''))
  const [capacity, setCapacity] = useState(state.kind === 'space' ? String(state.capacity) : '')
  const [submitting, setSubmitting] = useState(false)
  const f = useFieldErrors<Field>('edit')
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()

  const titles = { building: t('Hierarchy:EditBuildingTitle'), floor: t('Hierarchy:EditFloorTitle'), space: t('Hierarchy:EditSpaceTitle') }

  // Every problem at once, each under its own field — not the first one found.
  function validate(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {}
    if (!names[defaultLanguage]?.trim()) errors.name = t('Hierarchy:NameRequired')
    if (state.kind === 'floor' && floorNumber.trim() && !Number.isInteger(Number(floorNumber))) {
      errors.floorNumber = t('Hierarchy:FloorNumberWhole')
    }
    if (state.kind === 'space') {
      const parsed = Number(capacity)
      if (!capacity.trim() || !Number.isFinite(parsed) || parsed <= 0) errors.capacity = t('Hierarchy:CapacityPositive')
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
        await updateBuilding(token, state.id, {
          names: toNameList(names),
          addresses: toAddressList(names, addresses),
          buildingNumber: buildingNumber.trim() || null,
          timezone,
        })
      } else if (state.kind === 'floor') {
        await updateFloor(token, state.id, { names: toNameList(names), floorNumber: floorNumber.trim() ? Number(floorNumber) : null })
      } else {
        const input = { names: toNameList(names), spaceTypeId, capacity: Number(capacity) }
        // A lower capacity can leave bookings for more people behind: ask first.
        const impact = await getSpaceUpdateImpact(token, state.id, input)
        let cancelAffectedBookings = false
        if (impact.count > 0) {
          const choice = await askImpact({ mode: 'change', impact, loadMore: (skip) => getSpaceUpdateImpact(token, state.id, input, skip) })
          if (!choice) {
            setSubmitting(false)
            return
          }
          cancelAffectedBookings = choice === 'cancel'
        }
        await updateSpace(token, state.id, { ...input, cancelAffectedBookings })
      }

      onSaved()
      onClose()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : t('Error:Generic'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !submitting && onClose()}>
      {impactPrompt}
      <DialogContent>
        <form {...f.form} onSubmit={handleSubmit} noValidate>
          <DialogHeader>
            <DialogTitle>{titles[state.kind]}</DialogTitle>
            <DialogDescription>{t('Hierarchy:EditDetailsDetail')}</DialogDescription>
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
                maxLength={MAX_NAME_LENGTH}
                autoFocus
                languageNote={state.kind === 'building' ? addressNote(addresses, t('Hierarchy:HasAddress')) : undefined}
              />
            </div>
            {state.kind === 'building' && (
              <div className="sm:col-span-2">
                <LocalizedAddressField
                  id="edit-address"
                  value={addresses}
                  onChange={setAddresses}
                  names={names}
                  language={language}
                  maxLength={MAX_ADDRESS_LENGTH}
                />
              </div>
            )}

            {state.kind === 'building' && (
              <div className="grid gap-2">
                <Label htmlFor="edit-buildingNumber">{t('Hierarchy:BuildingNumber')}</Label>
                <Input
                  id="edit-buildingNumber"
                  className="tabular-nums"
                  value={buildingNumber}
                  onChange={(e) => setBuildingNumber(e.target.value)}
                />
              </div>
            )}

            {state.kind === 'floor' && (
              <div className="grid gap-2">
                <Label htmlFor={f.id('floorNumber')}>{t('Hierarchy:FloorNumber')}</Label>
                <Input {...f.field('floorNumber')} className="font-mono" value={floorNumber} onChange={(e) => setFloorNumber(e.target.value)} />
                {f.error('floorNumber')}
              </div>
            )}

            {state.kind === 'space' && (
              <div className="grid gap-2">
                <Label htmlFor={f.id('capacity')}>
                  {t('Hierarchy:Capacity')}<span className="text-destructive">*</span>
                </Label>
                <Input {...f.field('capacity')} className="font-mono" value={capacity} onChange={(e) => setCapacity(e.target.value)} />
                {f.error('capacity')}
              </div>
            )}

            {state.kind === 'building' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor="edit-timezone">
                  {t('Timezone:Label')}<span className="text-destructive">*</span>
                </Label>
                <TimezonePicker id="edit-timezone" value={timezone} onChange={setTimezone} />
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
              {submitting ? t('Common:Saving') : t('Hierarchy:SaveDetails')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
