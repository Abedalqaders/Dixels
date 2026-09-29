import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useFieldErrors } from '@/components/FieldError'
import { TimezonePicker } from '@/components/TimezonePicker'
import { ApiError, updateBuilding, updateFloor, updateSpace, getSpaceUpdateImpact } from '@/features/space-management/api/spaceManagementApi'
import type { SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'

// The identity-fields counterpart to AddNodeModal — Name/BuildingNumber/Timezone (Building),
// Name/FloorNumber (Floor), Name/SpaceType/Capacity (Space). Deliberately separate from the
// constraints page: these hit updateBuilding/updateFloor/updateSpace, not the constraints
// endpoints, and none of the three identity DTOs carry a ConcurrencyStamp — the backend does
// a plain overwrite here, unlike the constraints save.
//
// Same shadcn Dialog as AddNodeModal: focus stays inside, Escape and the overlay close it,
// and the title is announced — none of which the old hand-rolled overlay did.
export type EditDetailsState =
  | { kind: 'building'; id: string; name: string; buildingNumber: string | null; timezone: string }
  | { kind: 'floor'; id: string; name: string; floorNumber: number | null }
  | { kind: 'space'; id: string; name: string; spaceTypeId: string; capacity: number }
  | null

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
  const [name, setName] = useState(state.name)
  const [buildingNumber, setBuildingNumber] = useState(state.kind === 'building' ? (state.buildingNumber ?? '') : '')
  const [timezone, setTimezone] = useState(state.kind === 'building' ? state.timezone : 'UTC')
  const [floorNumber, setFloorNumber] = useState(state.kind === 'floor' ? String(state.floorNumber ?? '') : '')
  const [spaceTypeId, setSpaceTypeId] = useState(state.kind === 'space' ? state.spaceTypeId : (spaceTypes[0]?.id ?? ''))
  const [capacity, setCapacity] = useState(state.kind === 'space' ? String(state.capacity) : '')
  const [submitting, setSubmitting] = useState(false)
  const f = useFieldErrors<Field>('edit')
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()

  const titles = { building: 'Edit building details', floor: 'Edit floor details', space: 'Edit space details' }

  // Every problem at once, each under its own field — not the first one found.
  function validate(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {}
    if (!name.trim()) errors.name = 'Name is required.'
    if (state.kind === 'floor' && floorNumber.trim() && !Number.isInteger(Number(floorNumber))) {
      errors.floorNumber = 'Floor number must be a whole number.'
    }
    if (state.kind === 'space') {
      const parsed = Number(capacity)
      if (!capacity.trim() || !Number.isFinite(parsed) || parsed <= 0) errors.capacity = 'Capacity must be a positive number.'
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
        await updateBuilding(token, state.id, { name: name.trim(), buildingNumber: buildingNumber.trim() || null, timezone })
      } else if (state.kind === 'floor') {
        await updateFloor(token, state.id, { name: name.trim(), floorNumber: floorNumber.trim() ? Number(floorNumber) : null })
      } else {
        const input = { name: name.trim(), spaceTypeId, capacity: Number(capacity) }
        // A lower capacity can leave bookings for more people behind: ask first.
        const impact = await getSpaceUpdateImpact(token, state.id, input)
        let cancelAffectedBookings = false
        if (impact.count > 0) {
          const choice = await askImpact({ mode: 'change', impact })
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
      onError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
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
            <DialogDescription>Rules and hours are edited on the constraints page; this is what it's called and where it is.</DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor={f.id('name')}>
                Name<span className="text-destructive">*</span>
              </Label>
              <Input {...f.field('name')} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
              {f.error('name')}
            </div>

            {state.kind === 'building' && (
              <div className="grid gap-2">
                <Label htmlFor="edit-buildingNumber">Building number</Label>
                <Input
                  id="edit-buildingNumber"
                  className="font-mono"
                  value={buildingNumber}
                  onChange={(e) => setBuildingNumber(e.target.value)}
                />
              </div>
            )}

            {state.kind === 'floor' && (
              <div className="grid gap-2">
                <Label htmlFor={f.id('floorNumber')}>Floor number</Label>
                <Input {...f.field('floorNumber')} className="font-mono" value={floorNumber} onChange={(e) => setFloorNumber(e.target.value)} />
                {f.error('floorNumber')}
              </div>
            )}

            {state.kind === 'space' && (
              <div className="grid gap-2">
                <Label htmlFor={f.id('capacity')}>
                  Capacity<span className="text-destructive">*</span>
                </Label>
                <Input {...f.field('capacity')} className="font-mono" value={capacity} onChange={(e) => setCapacity(e.target.value)} />
                {f.error('capacity')}
              </div>
            )}

            {state.kind === 'building' && (
              <div className="grid gap-2 sm:col-span-2">
                <Label htmlFor="edit-timezone">
                  Timezone<span className="text-destructive">*</span>
                </Label>
                <TimezonePicker id="edit-timezone" value={timezone} onChange={setTimezone} />
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
              {submitting ? 'Saving…' : 'Save details'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
