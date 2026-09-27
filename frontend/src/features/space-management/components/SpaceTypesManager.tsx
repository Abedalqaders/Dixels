import { useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError, createSpaceType, deleteSpaceType, updateSpaceType } from '../api/spaceManagementApi'
import type { SpaceTypeDto } from '../api/spaceManagementApi'
import { ICONS, iconKeyToIconName } from './spaceTypeIcons'

// Never built in the mock (it only ever showed the space-type dropdown, not a way to
// manage the types behind it) — a real CRUD screen, since createSpaceType/updateSpaceType/
// deleteSpaceType all existed on the backend with nothing in the UI ever calling them.
//
// The icon set is deliberately closed to these 4 (see the backend's IconKey enum doc
// comment) rather than an arbitrary upload — picking one is a click on a swatch below, not
// a text dropdown, so it actually reads as "choosing an icon" instead of easy to miss.

const ICON_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: 'Meeting room' },
  { value: 1, label: 'Focus pod' },
  { value: 2, label: 'Desk' },
  { value: 3, label: 'Generic' },
]

function IconPicker({ value, onChange, disabled }: { value: number; onChange: (v: number) => void; disabled?: boolean }) {
  return (
    <div className="iconpicker">
      {ICON_OPTIONS.map((o) => (
        <button
          key={o.value}
          type="button"
          className={`iconswatch${value === o.value ? ' on' : ''}`}
          title={o.label}
          aria-label={o.label}
          aria-pressed={value === o.value}
          disabled={disabled}
          onClick={() => onChange(o.value)}
        >
          {ICONS[iconKeyToIconName(o.value)]}
        </button>
      ))}
    </div>
  )
}

interface ManageSpaceTypesModalProps {
  token: string
  spaceTypes: SpaceTypeDto[]
  onClose: () => void
  onChanged: () => void
  onError: (message: string) => void
  onSuccess: (message: string) => void
}

export function ManageSpaceTypesModal({ token, spaceTypes, onClose, onChanged, onError, onSuccess }: ManageSpaceTypesModalProps) {
  const [newName, setNewName] = useState('')
  const [newIconKey, setNewIconKey] = useState(0)
  const [adding, setAdding] = useState(false)

  async function handleAdd(e: FormEvent) {
    e.preventDefault()
    if (!newName.trim()) return

    setAdding(true)
    try {
      await createSpaceType(token, { name: newName.trim(), iconKey: newIconKey })
      setNewName('')
      setNewIconKey(0)
      onSuccess('Space type added.')
      onChanged()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
    } finally {
      setAdding(false)
    }
  }

  return (
    <div className="overlay show">
      <div className="modal wide">
        <h3>Manage space types</h3>
        <p className="sub">Space types are shared across every building — renaming or re-icon-ing one updates it everywhere it's used.</p>

        <div className="typelist">
          {spaceTypes.length === 0 && <p className="noteline">No space types yet — add one below.</p>}
          {spaceTypes.map((st) => (
            <SpaceTypeRow key={st.id} spaceType={st} token={token} onChanged={onChanged} onError={onError} onSuccess={onSuccess} />
          ))}
        </div>

        <form className="addtype" onSubmit={handleAdd}>
          <div className="field">
            <span className="lbl">Add a space type</span>
            <input className="ctrl" value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="e.g. Phone booth" />
          </div>
          <div className="field">
            <span className="lbl">Icon</span>
            <IconPicker value={newIconKey} onChange={setNewIconKey} disabled={adding} />
          </div>
          <div className="modalfoot" style={{ justifyContent: 'flex-start' }}>
            <button type="submit" className="btn sec" disabled={adding || !newName.trim()}>
              {adding ? 'Adding…' : '+ Add type'}
            </button>
          </div>
        </form>

        <div className="modalfoot">
          <button type="button" className="btn" onClick={onClose}>
            Done
          </button>
        </div>
      </div>
    </div>
  )
}

interface SpaceTypeRowProps {
  spaceType: SpaceTypeDto
  token: string
  onChanged: () => void
  onError: (message: string) => void
  onSuccess: (message: string) => void
}

function SpaceTypeRow({ spaceType, token, onChanged, onError, onSuccess }: SpaceTypeRowProps) {
  const [name, setName] = useState(spaceType.name)
  const [iconKey, setIconKey] = useState(spaceType.iconKey)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const dirty = name.trim() !== spaceType.name || iconKey !== spaceType.iconKey
  const busy = saving || deleting

  async function handleSave() {
    if (!name.trim()) {
      onError('Name is required.')
      return
    }

    setSaving(true)
    try {
      await updateSpaceType(token, spaceType.id, { name: name.trim(), iconKey })
      onSuccess('Space type updated.')
      onChanged()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!window.confirm(`Delete "${spaceType.name}"? This can't be undone.`)) return

    setDeleting(true)
    try {
      await deleteSpaceType(token, spaceType.id)
      onSuccess('Space type deleted.')
      onChanged()
    } catch (err) {
      // Surfaces the backend's real message, e.g. the SpaceTypeInUse block, rather than a
      // generic failure — the admin needs to know *why* before they can act on it.
      onError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
    } finally {
      setDeleting(false)
    }
  }

  return (
    <div className="typerow">
      <div className="typerow-top">
        <input className="ctrl" value={name} onChange={(e) => setName(e.target.value)} disabled={busy} />
        <div className="typerow-actions">
          <button type="button" className="btn sm sec" onClick={handleSave} disabled={!dirty || busy}>
            {saving ? 'Saving…' : 'Save'}
          </button>
          <button type="button" className="btn sm sec" onClick={handleDelete} disabled={busy}>
            {deleting ? 'Deleting…' : 'Delete'}
          </button>
        </div>
      </div>
      <IconPicker value={iconKey} onChange={setIconKey} disabled={busy} />
    </div>
  )
}
