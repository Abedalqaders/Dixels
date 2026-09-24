import { useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError, updateBuilding, updateFloor, updateSpace } from '../api/spaceManagementApi'
import type { SpaceTypeDto } from '../api/spaceManagementApi'

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

export function EditDetailsModal({ state, token, spaceTypes, onClose, onSaved, onError }: EditDetailsModalProps) {
  const [name, setName] = useState(state.name)
  const [buildingNumber, setBuildingNumber] = useState(state.kind === 'building' ? state.buildingNumber ?? '' : '')
  const [timezone, setTimezone] = useState(state.kind === 'building' ? state.timezone : TIMEZONES[0])
  const [floorNumber, setFloorNumber] = useState(state.kind === 'floor' ? String(state.floorNumber ?? '') : '')
  const [spaceTypeId, setSpaceTypeId] = useState(state.kind === 'space' ? state.spaceTypeId : (spaceTypes[0]?.id ?? ''))
  const [capacity, setCapacity] = useState(state.kind === 'space' ? String(state.capacity) : '')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const titles = { building: 'Edit building details', floor: 'Edit floor details', space: 'Edit space details' }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setValidationError(null)

    if (!name.trim()) {
      setValidationError('Name is required.')
      return
    }

    setSubmitting(true)
    try {
      if (state.kind === 'building') {
        await updateBuilding(token, state.id, { name: name.trim(), buildingNumber: buildingNumber.trim() || null, timezone })
      } else if (state.kind === 'floor') {
        await updateFloor(token, state.id, { name: name.trim(), floorNumber: floorNumber.trim() ? Number(floorNumber) : null })
      } else {
        const parsedCapacity = Number(capacity)
        if (!Number.isFinite(parsedCapacity) || parsedCapacity <= 0) {
          setValidationError('Capacity must be a positive number.')
          setSubmitting(false)
          return
        }
        if (!spaceTypeId) {
          setValidationError('Choose a space type.')
          setSubmitting(false)
          return
        }
        await updateSpace(token, state.id, { name: name.trim(), spaceTypeId, capacity: parsedCapacity })
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
      <form className="modal" onSubmit={handleSubmit}>
        <h3>{titles[state.kind]}</h3>
        <div className="row2">
          <div className="field">
            <span className="lbl">
              Name<span className="req">*</span>
            </span>
            <input className="ctrl" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          </div>
          {state.kind === 'building' && (
            <div className="field">
              <span className="lbl">Building number</span>
              <input className="ctrl mono" value={buildingNumber} onChange={(e) => setBuildingNumber(e.target.value)} />
            </div>
          )}
          {state.kind === 'floor' && (
            <div className="field">
              <span className="lbl">Floor number</span>
              <input className="ctrl mono" value={floorNumber} onChange={(e) => setFloorNumber(e.target.value)} />
            </div>
          )}
          {state.kind === 'space' && (
            <div className="field">
              <span className="lbl">
                Capacity<span className="req">*</span>
              </span>
              <input className="ctrl mono" value={capacity} onChange={(e) => setCapacity(e.target.value)} />
            </div>
          )}
        </div>
        {state.kind === 'building' && (
          <div className="row2">
            <div className="field">
              <span className="lbl">
                Timezone<span className="req">*</span>
              </span>
              <select className="ctrl" value={timezone} onChange={(e) => setTimezone(e.target.value)}>
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
              <span className="lbl">
                Type<span className="req">*</span>
              </span>
              <select className="ctrl" value={spaceTypeId} onChange={(e) => setSpaceTypeId(e.target.value)}>
                {spaceTypes.map((st) => (
                  <option key={st.id} value={st.id}>
                    {st.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}
        {validationError && <p className="noteline">{validationError}</p>}
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
