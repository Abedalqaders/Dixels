import { useMemo, useState } from 'react'
import { Sidebar } from '../components/Sidebar'
import '../styles/tokens.css'
import '../styles/base.css'
import '../styles/admin.css'

type Space = { name: string; seats: number; type: 'meeting-room' | 'focus-pod' | 'desk'; blocked?: boolean }
type Floor = { name: string; floorNum: number; spaceCount: number; spaces: Space[] }

// Static sample data matching CONSTRAINTS.md's worked examples - no backend
// yet, this is the visual design only.
const BUILDING = { name: 'Ridge House', bnum: 'RH-01', tz: 'Asia/Amman' }
const FLOORS: Floor[] = [
  { name: 'Level 1', floorNum: 1, spaceCount: 9, spaces: [] },
  {
    name: 'Level 2',
    floorNum: 2,
    spaceCount: 22,
    spaces: [{ name: 'Focus Pod 2-04', seats: 1, type: 'focus-pod', blocked: true }],
  },
  {
    name: 'Level 3',
    floorNum: 3,
    spaceCount: 21,
    spaces: [
      { name: 'Meeting Room 3B', seats: 8, type: 'meeting-room' },
      { name: 'Meeting Room 3C', seats: 4, type: 'meeting-room' },
    ],
  },
  { name: 'Level 4 — Engineering', floorNum: 4, spaceCount: 16, spaces: [] },
]

const ICONS = {
  building: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M3 17V7l7-4 7 4v10z" /></svg>
  ),
  floor: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><path d="M3.5 6h13M3.5 10h13M3.5 14h13" /></svg>
  ),
  'meeting-room': (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><circle cx="7.2" cy="6.8" r="2.2" /><path d="M2.8 15.3c.3-2.6 2-4 4.4-4s4 1.4 4.4 4" /><circle cx="14.3" cy="7.6" r="1.7" /><path d="M12.2 11.4c.9-.4 1.9-.4 2.7.2.9.6 1.5 1.8 1.7 3.2" /></svg>
  ),
  'focus-pod': (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M4.5 17V9.5a5.5 5.5 0 0 1 11 0V17" /><path d="M3 17h14" /></svg>
  ),
  desk: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="5" y="3.5" width="10" height="7" rx="1" /><path d="M8 14h4M10 10.5V14" /><path d="M3 17h14" /></svg>
  ),
}

const DetailsIcon = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><circle cx="10" cy="10" r="7.2" /><path d="M10 9.4v4" /><circle cx="10" cy="6.6" r=".9" fill="currentColor" stroke="none" /></svg>
)
const PencilIcon = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M12.7 3.3a1.5 1.5 0 0 1 2.1 0l1.9 1.9a1.5 1.5 0 0 1 0 2.1L7 17H3v-4L12.7 3.3z" /><path d="M11 5.3l3.7 3.7" /></svg>
)
const PlusIcon = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><path d="M10 4.5v11M4.5 10h11" /></svg>
)
const ChevronIcon = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d="M5.5 8 10 12.5 14.5 8" /></svg>
)

type ModalState = { kind: 'building' | 'floor' | 'space'; parentName?: string } | null

export function AdminBuildings() {
  const [search, setSearch] = useState('')
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())
  const [modal, setModal] = useState<ModalState>(null)

  const q = search.trim().toLowerCase()

  const visibleFloors = useMemo(() => {
    if (!q) return FLOORS.map((f) => ({ ...f, spaces: f.spaces }))
    return FLOORS.map((f) => {
      const floorMatches = f.name.toLowerCase().includes(q)
      const matchingSpaces = f.spaces.filter((s) => s.name.toLowerCase().includes(q))
      return { ...f, spaces: floorMatches ? f.spaces : matchingSpaces }
    }).filter((f) => f.name.toLowerCase().includes(q) || f.spaces.length > 0)
  }, [q])

  const totalSpaces = FLOORS.reduce((sum, f) => sum + f.spaceCount, 0)

  function toggleFloor(name: string) {
    setCollapsed((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Space management</h1>
            <p className="lead">
              1 building · {FLOORS.length} floors · {totalSpaces} bookable spaces. Hover a row for its actions, use
              the arrow to collapse a floor, or search to jump straight to a space.
            </p>
          </div>

          <section className="card" id="hierarchy">
            <div className="cardhead">
              <h2 className="sectiontitle">Hierarchy</h2>
              <div className="treetools">
                <div className="searchbox">
                  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><circle cx="9" cy="9" r="5.5" /><path d="M13 13l4 4" /></svg>
                  <input
                    type="text"
                    placeholder="Filter by name…"
                    autoComplete="off"
                    aria-label="Filter buildings, floors and spaces"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </div>
                <button className="btn sm sec" onClick={() => setModal({ kind: 'building' })}>+ Building</button>
              </div>
            </div>
            <p className="treelegend">
              <span>{ICONS['meeting-room']} Meeting room</span>
              <span>{ICONS['focus-pod']} Focus pod</span>
              <span>{ICONS.desk} Desk</span>
            </p>

            <div className="tree">
              <div className="node l1" data-level="building">
                {ICONS.building}
                <span className="lbl2">{BUILDING.name}</span>
                <span className="m">{BUILDING.tz} · {BUILDING.bnum}</span>
                <span className="actions">
                  <button className="rowbtn" title={`Edit ${BUILDING.name} details`} aria-label={`Edit ${BUILDING.name} details`}><DetailsIcon /></button>
                  <button className="rowbtn" title={`Edit ${BUILDING.name} constraints`} aria-label={`Edit ${BUILDING.name} constraints`}><PencilIcon /></button>
                  <button className="rowbtn" title="Add floor" aria-label="Add floor" onClick={() => setModal({ kind: 'floor', parentName: BUILDING.name })}><PlusIcon /></button>
                </span>
              </div>

              {visibleFloors.map((floor) => {
                const isCollapsed = collapsed.has(floor.name)
                return (
                  <div key={floor.name}>
                    <div className={`node l2${floor.spaces.length === 0 ? ' empty' : ''}${isCollapsed ? ' collapsed' : ''}`} data-level="floor">
                      <button
                        className="chev"
                        title={isCollapsed ? `Expand ${floor.name}` : `Collapse ${floor.name}`}
                        aria-label={isCollapsed ? `Expand ${floor.name}` : `Collapse ${floor.name}`}
                        aria-expanded={!isCollapsed}
                        onClick={() => toggleFloor(floor.name)}
                      >
                        <ChevronIcon />
                      </button>
                      {ICONS.floor}
                      <span className="lbl2">{floor.name}</span>
                      <span className="m">Floor {floor.floorNum} · {floor.spaceCount} spaces</span>
                      <span className="actions">
                        <button className="rowbtn" title={`Edit ${floor.name} details`} aria-label={`Edit ${floor.name} details`}><DetailsIcon /></button>
                        <button className="rowbtn" title={`Edit ${floor.name} constraints`} aria-label={`Edit ${floor.name} constraints`}><PencilIcon /></button>
                        <button
                          className="rowbtn"
                          title={`Add space to ${floor.name}`}
                          aria-label={`Add space to ${floor.name}`}
                          onClick={() => setModal({ kind: 'space', parentName: floor.name })}
                        >
                          <PlusIcon />
                        </button>
                      </span>
                    </div>

                    {!isCollapsed &&
                      floor.spaces.map((space) => (
                        <div className="node l3" data-level="space" key={space.name}>
                          {ICONS[space.type]}
                          <span className="lbl2">{space.name}</span>
                          {space.blocked ? (
                            <span className="badge blocked">Out of Order</span>
                          ) : (
                            <span className="m">
                              {space.type === 'meeting-room' ? 'Meeting room' : space.type === 'focus-pod' ? 'Focus pod' : 'Desk'} · seats {space.seats}
                            </span>
                          )}
                          <span className="actions">
                            <button className="rowbtn" title={`Edit ${space.name} details`} aria-label={`Edit ${space.name} details`}><DetailsIcon /></button>
                            <button className="rowbtn" title={`Edit ${space.name} constraints`} aria-label={`Edit ${space.name} constraints`}><PencilIcon /></button>
                          </span>
                        </div>
                      ))}
                  </div>
                )
              })}

              {q && visibleFloors.length === 0 && (
                <p className="treeempty">No floors or spaces match "{search}".</p>
              )}
            </div>
          </section>
        </div>
      </div>

      {modal && <AddNodeModal state={modal} onClose={() => setModal(null)} />}
    </div>
  )
}

function AddNodeModal({ state, onClose }: { state: NonNullable<ModalState>; onClose: () => void }) {
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
