import { useState } from 'react'
import type { FormEvent } from 'react'
import { useFieldErrors } from '@/components/FieldError'
import { ApiError, updateBuilding, updateFloor, updateSpace, getSpaceUpdateImpact } from '@/features/space-management/api/spaceManagementApi'
import type { SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'

// The identity-fields counterpart to AddNodeModal — Name/BuildingNumber/Timezone (Building),
// Name/FloorNumber (Floor), Name/SpaceType/Capacity (Space). Deliberately separate from the
// constraints page: these hit updateBuilding/updateFloor/updateSpace, not the constraints
// endpoints, and none of the three identity DTOs carry a ConcurrencyStamp — the backend does
// a plain overwrite here, unlike the constraints save.
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

const TIMEZONES = ['Asia/Amman', 'Europe/London', 'America/New_York', 'UTC']

type Field = 'name' | 'floorNumber' | 'capacity' | 'type'

export function EditDetailsModal({ state, token, spaceTypes, onClose, onSaved, onError }: EditDetailsModalProps) {
  const [name, setName] = useState(state.name)
  const [buildingNumber, setBuildingNumber] = useState(state.kind === 'building' ? state.buildingNumber ?? '' : '')
  const [timezone, setTimezone] = useState(state.kind === 'building' ? state.timezone : TIMEZONES[0])
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
    <div className="overlay show">
      {impactPrompt}
      <form ref={f.formRef} className="modal" onSubmit={handleSubmit} noValidate>
        <h3>{titles[state.kind]}</h3>
        <div className="row2">
          <div className="field">
            <label className="lbl" htmlFor={f.id('name')}>
              Name<span className="req">*</span>
            </label>
            <input {...f.field('name')} className="ctrl" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
            {f.error('name')}
          </div>
          {state.kind === 'building' && (
            <div className="field">
              <label className="lbl" htmlFor="edit-buildingNumber">
                Building number
              </label>
              <input id="edit-buildingNumber" className="ctrl mono" value={buildingNumber} onChange={(e) => setBuildingNumber(e.target.value)} />
            </div>
          )}
          {state.kind === 'floor' && (
            <div className="field">
              <label className="lbl" htmlFor={f.id('floorNumber')}>
                Floor number
              </label>
              <input {...f.field('floorNumber')} className="ctrl mono" value={floorNumber} onChange={(e) => setFloorNumber(e.target.value)} />
              {f.error('floorNumber')}
            </div>
          )}
          {state.kind === 'space' && (
            <div className="field">
              <label className="lbl" htmlFor={f.id('capacity')}>
                Capacity<span className="req">*</span>
              </label>
              <input {...f.field('capacity')} className="ctrl mono" value={capacity} onChange={(e) => setCapacity(e.target.value)} />
              {f.error('capacity')}
            </div>
          )}
        </div>
        {state.kind === 'building' && (
          <div className="row2">
            <div className="field">
              <label className="lbl" htmlFor="edit-timezone">
                Timezone<span className="req">*</span>
              </label>
              <select id="edit-timezone" className="ctrl" value={timezone} onChange={(e) => setTimezone(e.target.value)}>
                {TIMEZONES.map((tz) => (
                  <option key={tz} value={tz}>
                    {tz}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}
        {state.kind === 'space' && (
          <div className="row2">
            <div className="field">
              <label className="lbl" htmlFor={f.id('type')}>
                Type<span className="req">*</span>
              </label>
              <select {...f.field('type')} className="ctrl" value={spaceTypeId} onChange={(e) => setSpaceTypeId(e.target.value)}>
                {spaceTypes.map((st) => (
                  <option key={st.id} value={st.id}>
                    {st.name}
                  </option>
                ))}
              </select>
              {f.error('type')}
            </div>
          </div>
        )}
        <div className="modalfoot">
          <button type="button" className="btn sec" onClick={onClose} disabled={submitting}>
            Cancel
          </button>
          <button type="submit" className="btn" disabled={submitting}>
            {submitting ? 'Saving…' : 'Save details'}
          </button>
        </div>
      </form>
    </div>
  )
}
