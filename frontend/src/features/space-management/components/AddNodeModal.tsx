import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useFieldErrors } from '@/components/FieldError'
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
  const [name, setName] = useState('')
  const [meta, setMeta] = useState('')
  const [timezone, setTimezone] = useState('UTC')
  const [spaceTypeId, setSpaceTypeId] = useState(spaceTypes[0]?.id ?? '')
  const [submitting, setSubmitting] = useState(false)
  const f = useFieldErrors<Field>('add')

  const titles = { building: 'Add building', floor: 'Add floor', space: 'Add space' }
  const metaLabel = state.kind === 'building' ? 'Building number' : state.kind === 'floor' ? 'Floor number' : 'Capacity'
  const metaPlaceholder = state.kind === 'building' ? 'e.g. RH-02' : state.kind === 'floor' ? 'e.g. 5' : 'e.g. 6'
  const metaRequired = state.kind === 'space' // Building/Floor number are optional; Capacity is required.

  // Every problem at once, each under its own field — not the first one found.
  function validate(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {}
    if (!name.trim()) errors.name = 'Name is required.'
    if (state.kind === 'floor' && meta.trim() && !Number.isInteger(Number(meta))) {
      errors.meta = 'Floor number must be a whole number.'
    }
    if (state.kind === 'space') {
      const capacity = Number(meta)
      if (!meta.trim() || !Number.isFinite(capacity) || capacity <= 0) errors.meta = 'Capacity must be a positive number.'
      if (!spaceTypeId) errors.type = 'Choose a space type.'
    }
    return errors
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const errors = validate()
    if (Object.keys(errors).length > 0) {
      f.setErrors(errors)
      return
    }
    f.clear()

    setSubmitting(true)
    try {
      if (state.kind === 'building') {
        await createBuilding(token, {
          name: name.trim(),
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
          name: name.trim(),
          floorNumber: meta.trim() ? Number(meta) : null,
        })
      } else {
        await createSpace(token, { floorId: state.parentId, name: name.trim(), spaceTypeId, capacity: Number(meta) })
      }

      onCreated()
      onClose()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
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
            {state.kind !== 'building' && <DialogDescription>Added under {state.parentName}.</DialogDescription>}
          </DialogHeader>

          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor={f.id('name')}>
                Name<span className="text-destructive">*</span>
              </Label>
              <Input
                {...f.field('name')}
                placeholder={state.kind === 'floor' ? 'e.g. Level 5' : 'Name'}
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoFocus
              />
              {f.error('name')}
            </div>
            <div className="grid gap-2">
              <Label htmlFor={f.id('meta')}>
                {metaLabel}
                {metaRequired && <span className="text-destructive">*</span>}
              </Label>
              <Input
                {...f.field('meta')}
                className="font-mono"
                placeholder={metaPlaceholder}
                value={meta}
                onChange={(e) => setMeta(e.target.value)}
              />
              {f.error('meta')}
            </div>

            {state.kind === 'building' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor="add-timezone">
                  Timezone<span className="text-destructive">*</span>
                </Label>
                <TimezonePicker id="add-timezone" value={timezone} onChange={setTimezone} />
              </div>
            )}

            {state.kind === 'space' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor={f.id('type')}>
                  Type<span className="text-destructive">*</span>
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
              Cancel
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? 'Adding…' : 'Add'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
