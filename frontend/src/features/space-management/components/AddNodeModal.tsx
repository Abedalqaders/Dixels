export type ModalState = { kind: 'building' | 'floor' | 'space'; parentName?: string } | null

export function AddNodeModal({ state, onClose }: { state: NonNullable<ModalState>; onClose: () => void }) {
  const titles = { building: 'Add building', floor: 'Add floor', space: 'Add space' }
  const metaLabel = state.kind === 'building' ? 'Building number' : state.kind === 'floor' ? 'Floor number' : 'Capacity'
  const metaPlaceholder = state.kind === 'building' ? 'e.g. RH-02' : state.kind === 'floor' ? 'e.g. 5' : 'e.g. 6'

  return (
    <div className="overlay show">
      <div className="modal">
        <h3>{titles[state.kind]}</h3>
        {state.parentName && <p className="sub">Added under {state.parentName}.</p>}
        <div className="row2">
          <div className="field">
            <span className="lbl">Name</span>
            <input className="ctrl" placeholder={state.kind === 'floor' ? 'e.g. Level 5' : 'Name'} />
          </div>
          <div className="field">
            <span className="lbl">{metaLabel}</span>
            <input className="ctrl mono" placeholder={metaPlaceholder} />
          </div>
        </div>
        {state.kind === 'building' && (
          <div className="row2">
            <div className="field">
              <span className="lbl">Timezone</span>
              <select className="ctrl" defaultValue="Asia/Amman">
                <option>Asia/Amman</option>
                <option>Europe/London</option>
                <option>America/New_York</option>
                <option>UTC</option>
              </select>
            </div>
          </div>
        )}
        {state.kind === 'space' && (
          <div className="row2">
            <div className="field">
              <span className="lbl">Type</span>
              <select className="ctrl" defaultValue="meeting-room">
                <option value="meeting-room">Meeting room</option>
                <option value="focus-pod">Focus pod</option>
                <option value="desk">Desk</option>
              </select>
            </div>
          </div>
        )}
        <div className="modalfoot">
          <button className="btn sec" onClick={onClose}>Cancel</button>
          <button className="btn" onClick={onClose} title="Not wired up to the backend yet">Add</button>
        </div>
      </div>
    </div>
  )
}
