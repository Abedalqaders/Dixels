import { useState } from 'react'
import type { FormEvent } from 'react'
import {
  ApiError,
  createBuilding,
  createFloor,
  createSpace,
} from '../api/spaceManagementApi'
import type { OperatingWindowDto, SpaceTypeDto } from '../api/spaceManagementApi'

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

export function AddNodeModal({ state, token, spaceTypes, onClose, onCreated, onError }: AddNodeModalProps) {
  const [name, setName] = useState('')
  const [meta, setMeta] = useState('')
  const [timezone, setTimezone] = useState('UTC')
  const [spaceTypeId, setSpaceTypeId] = useState(spaceTypes[0]?.id ?? '')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const titles = { building: 'Add building', floor: 'Add floor', space: 'Add space' }
  const metaLabel = state.kind === 'building' ? 'Building number' : state.kind === 'floor' ? 'Floor number' : 'Capacity'
  const metaPlaceholder = state.kind === 'building' ? 'e.g. RH-02' : state.kind === 'floor' ? 'e.g. 5' : 'e.g. 6'
  const metaRequired = state.kind === 'space' // Building/Floor number are optional; Capacity is required.

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
        const capacity = Number(meta)
        if (!Number.isFinite(capacity) || capacity <= 0) {
          setValidationError('Capacity must be a positive number.')
          setSubmitting(false)
          return
        }
        if (!spaceTypeId) {
          setValidationError('Choose a space type.')
          setSubmitting(false)
          return
        }
        await createSpace(token, { floorId: state.parentId, name: name.trim(), spaceTypeId, capacity })
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
    <div className="overlay show">
      <form className="modal" onSubmit={handleSubmit}>
        <h3>{titles[state.kind]}</h3>
        {state.kind !== 'building' && <p className="sub">Added under {state.parentName}.</p>}
        <div className="row2">
          <div className="field">
            <span className="lbl">
              Name<span className="req">*</span>
            </span>
            <input
              className="ctrl"
              placeholder={state.kind === 'floor' ? 'e.g. Level 5' : 'Name'}
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoFocus
            />
          </div>
          <div className="field">
            <span className="lbl">
              {metaLabel}
              {metaRequired && <span className="req">*</span>}
            </span>
            <input
              className="ctrl mono"
              placeholder={metaPlaceholder}
              value={meta}
              onChange={(e) => setMeta(e.target.value)}
            />
          </div>
        </div>
        {state.kind === 'building' && (
          <div className="row2">
            <div className="field">
              <span className="lbl">
                Timezone<span className="req">*</span>
              </span>
              <select className="ctrl" value={timezone} onChange={(e) => setTimezone(e.target.value)}>
                <option value="Asia/Amman">Asia/Amman</option>
                <option value="Europe/London">Europe/London</option>
                <option value="America/New_York">America/New_York</option>
                <option value="UTC">UTC</option>
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
            {submitting ? 'Adding…' : 'Add'}
          </button>
        </div>
      </form>
    </div>
  )
}
