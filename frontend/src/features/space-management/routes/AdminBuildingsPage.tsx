import { useMemo, useState } from 'react'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { ICONS } from '../components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, PlusIcon, ChevronIcon } from '../components/actionIcons'
import { AddNodeModal } from '../components/AddNodeModal'
import type { ModalState } from '../components/AddNodeModal'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'

type Space = { name: string; seats: number; type: 'meeting-room' | 'focus-pod' | 'desk'; blocked?: boolean }
type Floor = { name: string; floorNum: number; spaceCount: number; spaces: Space[] }

// Static sample data - no backend yet, this is the visual design only.
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

export function AdminBuildingsPage() {
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
                  <SearchIcon />
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
