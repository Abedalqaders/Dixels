import { useEffect, useRef, useState } from 'react'
import { Link, matchPath, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SearchIcon } from '../../../components/icons'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { getBuilding, getBuildings, getFloors } from '../api/spaceManagementApi'
import { useHierarchyChanged } from '../hierarchyEvents'
import { ICONS } from './spaceTypeIcons'
import { TreeSkeleton } from '../../../components/LoadingSkeletons'

const BUILDING_PAGE = 30
const FLOOR_PAGE = 50
const FLOOR_HIT_LIMIT = 20
const COLLAPSED_KEY = 'dixels.explorer.collapsed'

interface TreeItem {
  id: string
  name: string
}

interface Branch {
  items: TreeItem[]
  totalCount: number
  loading: boolean
  error: boolean
}

interface FloorHit {
  id: string
  name: string
  buildingId: string
  buildingName: string
}

const EMPTY_BRANCH: Branch = { items: [], totalCount: 0, loading: true, error: false }

function Chevron() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M8 5.5 12.5 10 8 14.5" />
    </svg>
  )
}

function PanelIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="14" height="12" rx="1.5" />
      <path d="M8 4v12" />
    </svg>
  )
}

function readCollapsed() {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}

function writeCollapsed(value: boolean) {
  try {
    if (value) localStorage.setItem(COLLAPSED_KEY, '1')
    else localStorage.removeItem(COLLAPSED_KEY)
  } catch {
    // Private window / blocked storage — the panel still toggles, it just won't be remembered.
  }
}

// Building → Floor tree beside the space-management pages. Built to stay cheap at both ends
// of the product — one small office or a portfolio of towers with 160 floors each:
// buildings load a page at a time, a building's floors are fetched only the first time it's
// expanded, search runs on the server and finds floors as well as buildings, and spaces
// never go in the tree at all (a floor can hold hundreds of desks, so they stay in the
// paged list on the right).
//
// A click navigates: building → its floors, floor → its spaces.
//
// Lives in SpaceManagementLayout, so it stays mounted (and keeps what it loaded) while the
// admin moves between pages.
export function SpaceExplorer() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const location = useLocation()

  // The layout sits above the page routes, so useParams() here wouldn't see :buildingId —
  // read the selection straight off the path instead.
  const floorMatch = matchPath('/admin/buildings/:buildingId/floors/:floorId/*', location.pathname)
  const buildingMatch = matchPath('/admin/buildings/:buildingId/*', location.pathname)
  const activeBuildingId = buildingMatch?.params.buildingId ?? ''
  const activeFloorId = floorMatch?.params.floorId ?? ''
  const rootIsCurrent = location.pathname === '/admin/buildings'

  function scopeLink(buildingId?: string, floorId?: string) {
    if (buildingId && floorId) return `/admin/buildings/${buildingId}/floors/${floorId}/spaces`
    if (buildingId) return `/admin/buildings/${buildingId}/floors`
    return '/admin/buildings'
  }

  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebouncedValue(query, 250).trim()
  const [buildings, setBuildings] = useState<Branch>(EMPTY_BRANCH)
  const [floorHits, setFloorHits] = useState<{ items: FloorHit[]; totalCount: number } | null>(null)
  const [floors, setFloors] = useState<Record<string, Branch>>({})
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set())
  // The open building when it isn't in the loaded page (e.g. #240 of 500) — shown pinned at
  // the top so the admin can always see where they are.
  const [pinned, setPinned] = useState<TreeItem | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  // Drops responses from a search the admin has already typed past.
  const buildingsRequest = useRef(0)

  useHierarchyChanged(() => {
    setFloors({})
    setPinned(null)
    setReloadKey((k) => k + 1)
  })

  useEffect(() => {
    const requestId = ++buildingsRequest.current
    setBuildings((b) => ({ ...b, loading: true, error: false }))
    setFloorHits(null)
    getBuildings(token, { filter: debouncedQuery || undefined, maxResultCount: BUILDING_PAGE })
      .then((r) => {
        if (requestId !== buildingsRequest.current) return
        setBuildings({ items: r.items.map(toItem), totalCount: r.totalCount, loading: false, error: false })
      })
      .catch(() => requestId === buildingsRequest.current && setBuildings({ ...EMPTY_BRANCH, loading: false, error: true }))

    // In a tall tower the floor is what the admin remembers ("Sky Lobby", "L42"), so a search
    // also looks for floors — by their own name only, since building-name matches are
    // already listed above.
    if (debouncedQuery) {
      getFloors(token, { filter: debouncedQuery, floorNameOnly: true, maxResultCount: FLOOR_HIT_LIMIT })
        .then((r) => {
          if (requestId !== buildingsRequest.current) return
          setFloorHits({
            items: r.items.map((f) => ({ id: f.id, name: f.name, buildingId: f.buildingId, buildingName: f.buildingName ?? '' })),
            totalCount: r.totalCount,
          })
        })
        .catch(() => requestId === buildingsRequest.current && setFloorHits({ items: [], totalCount: 0 }))
    }
  }, [token, debouncedQuery, reloadKey])

  function loadMoreBuildings() {
    const requestId = ++buildingsRequest.current
    setBuildings((b) => ({ ...b, loading: true }))
    getBuildings(token, { filter: debouncedQuery || undefined, skipCount: buildings.items.length, maxResultCount: BUILDING_PAGE })
      .then((r) => {
        if (requestId !== buildingsRequest.current) return
        setBuildings((b) => ({ items: [...b.items, ...r.items.map(toItem)], totalCount: r.totalCount, loading: false, error: false }))
      })
      .catch(() => requestId === buildingsRequest.current && setBuildings((b) => ({ ...b, loading: false, error: true })))
  }

  function fetchFloors(buildingId: string, skipCount: number) {
    setFloors((f) => ({ ...f, [buildingId]: { ...(f[buildingId] ?? EMPTY_BRANCH), loading: true, error: false } }))
    getFloors(token, { buildingId, skipCount, maxResultCount: FLOOR_PAGE })
      .then((r) =>
        setFloors((f) => ({
          ...f,
          [buildingId]: {
            items: [...(skipCount > 0 ? (f[buildingId]?.items ?? []) : []), ...r.items.map(toItem)],
            totalCount: r.totalCount,
            loading: false,
            error: false,
          },
        })),
      )
      .catch(() => setFloors((f) => ({ ...f, [buildingId]: { ...(f[buildingId] ?? EMPTY_BRANCH), loading: false, error: true } })))
  }

  function expand(id: string) {
    setExpanded((e) => (e.has(id) ? e : new Set(e).add(id)))
  }

  // Opening a building's page from anywhere (a list row, a link, Back) unfolds it here too.
  useEffect(() => {
    if (activeBuildingId) expand(activeBuildingId)
  }, [activeBuildingId])

  // A customer with a single building shouldn't have to click it open to see anything.
  const onlyBuildingId = !debouncedQuery && buildings.totalCount === 1 ? buildings.items[0]?.id : undefined
  useEffect(() => {
    if (onlyBuildingId) expand(onlyBuildingId)
  }, [onlyBuildingId])

  // Every expanded building without cached floors gets fetched — covers a fresh expand and
  // the reload after a hierarchy change (which clears the cache) with one rule.
  useEffect(() => {
    for (const id of expanded) if (!floors[id]) fetchFloors(id, 0)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expanded, floors, token])

  const activeIsListed = buildings.items.some((b) => b.id === activeBuildingId)
  useEffect(() => {
    if (!activeBuildingId || activeIsListed || buildings.loading || pinned?.id === activeBuildingId) return
    let cancelled = false
    getBuilding(token, activeBuildingId)
      .then((b) => !cancelled && setPinned(b.isDeleted ? null : toItem(b)))
      .catch(() => !cancelled && setPinned(null))
    return () => {
      cancelled = true
    }
  }, [token, activeBuildingId, activeIsListed, buildings.loading, pinned])

  function toggle(id: string) {
    setExpanded((e) => {
      const next = new Set(e)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function setPanelCollapsed(value: boolean) {
    setCollapsed(value)
    writeCollapsed(value)
  }

  if (collapsed) {
    return (
      <aside className="explorer collapsed" aria-label="Buildings and floors">
        <button type="button" className="xpanelbtn" aria-label="Show buildings panel" title="Show buildings panel" onClick={() => setPanelCollapsed(false)}>
          <PanelIcon />
        </button>
      </aside>
    )
  }

  const showPinned = pinned && pinned.id === activeBuildingId && !activeIsListed
  const visibleBuildings = showPinned ? [pinned, ...buildings.items] : buildings.items
  const hiddenBuildings = buildings.totalCount - buildings.items.length
  const hiddenFloorHits = floorHits ? floorHits.totalCount - floorHits.items.length : 0

  return (
    <aside className="explorer" aria-label="Buildings and floors">
      <div className="xhead">
        <div className="searchbox">
          <SearchIcon />
          <input
            type="text"
            placeholder="Find a building or floor…"
            autoComplete="off"
            aria-label="Find a building or floor"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>
        <button type="button" className="xpanelbtn" aria-label="Hide buildings panel" title="Hide buildings panel" onClick={() => setPanelCollapsed(true)}>
          <PanelIcon />
        </button>
      </div>

      <Link to={scopeLink()} className={`xrow root${rootIsCurrent ? ' on' : ''}`} aria-current={rootIsCurrent ? 'page' : undefined}>
        {ICONS.building}
        <span className="xlbl">All buildings</span>
        {!buildings.loading && !debouncedQuery && <span className="xcount">{buildings.totalCount}</span>}
      </Link>

      {debouncedQuery && <p className="xsection">Buildings</p>}
      <ul className="xtree" aria-busy={buildings.loading}>
        {visibleBuildings.map((building) => {
          const isOpen = expanded.has(building.id)
          const branch = floors[building.id]
          const isCurrent = building.id === activeBuildingId && !activeFloorId
          return (
            <li key={building.id}>
              <div className={`xrow${isCurrent ? ' on' : ''}`}>
                <button
                  type="button"
                  className={`xtoggle${isOpen ? ' open' : ''}`}
                  aria-expanded={isOpen}
                  aria-label={`${isOpen ? 'Collapse' : 'Expand'} ${building.name}`}
                  onClick={() => toggle(building.id)}
                >
                  <Chevron />
                </button>
                <Link to={scopeLink(building.id)} className="xlbl" title={building.name} aria-current={isCurrent ? 'page' : undefined}>
                  {building.name}
                </Link>
              </div>

              {isOpen && (
                <ul className="xfloors">
                  {branch?.items.map((floor) => (
                    <li key={floor.id}>
                      <Link
                        to={scopeLink(building.id, floor.id)}
                        className={`xrow floor${floor.id === activeFloorId ? ' on' : ''}`}
                        title={floor.name}
                        aria-current={floor.id === activeFloorId ? 'page' : undefined}
                      >
                        {ICONS.floor}
                        <span className="xlbl">{floor.name}</span>
                      </Link>
                    </li>
                  ))}
                  {branch?.loading && <li className="xnote">Loading floors…</li>}
                  {branch?.error && (
                    <li className="xnote">
                      Couldn't load floors.{' '}
                      <button type="button" className="xlink" onClick={() => fetchFloors(building.id, branch.items.length)}>
                        Retry
                      </button>
                    </li>
                  )}
                  {branch && !branch.loading && !branch.error && branch.items.length === 0 && <li className="xnote">No floors yet</li>}
                  {branch && !branch.loading && branch.items.length < branch.totalCount && (
                    <li>
                      <button type="button" className="xmore" onClick={() => fetchFloors(building.id, branch.items.length)}>
                        Show {Math.min(FLOOR_PAGE, branch.totalCount - branch.items.length)} more floors
                      </button>
                    </li>
                  )}
                </ul>
              )}
            </li>
          )
        })}
      </ul>

      {buildings.loading && buildings.items.length === 0 && <TreeSkeleton label="Loading buildings…" rows={4} />}
      {buildings.loading && buildings.items.length > 0 && <p className="xnote">Loading…</p>}
      {buildings.error && (
        <p className="xnote">
          Couldn't load buildings.{' '}
          <button type="button" className="xlink" onClick={() => setReloadKey((k) => k + 1)}>
            Retry
          </button>
        </p>
      )}
      {!buildings.loading && !buildings.error && buildings.items.length === 0 && (
        <p className="xnote">{debouncedQuery ? 'No buildings match.' : 'No buildings yet.'}</p>
      )}
      {!buildings.loading && hiddenBuildings > 0 && (
        <button type="button" className="xmore" onClick={loadMoreBuildings}>
          Show more ({hiddenBuildings} left)
        </button>
      )}

      {debouncedQuery && (
        <>
          <p className="xsection">Floors</p>
          {floorHits === null && <p className="xnote">Searching…</p>}
          {floorHits !== null && floorHits.items.length === 0 && <p className="xnote">No floors match.</p>}
          <ul className="xtree">
            {floorHits?.items.map((hit) => (
              <li key={hit.id}>
                <Link
                  to={scopeLink(hit.buildingId, hit.id)}
                  className={`xrow floor${hit.id === activeFloorId ? ' on' : ''}`}
                  title={`${hit.name} · ${hit.buildingName}`}
                  aria-current={hit.id === activeFloorId ? 'page' : undefined}
                >
                  {ICONS.floor}
                  <span className="xlbl">
                    {hit.name}
                    <span className="xsub">{hit.buildingName}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {hiddenFloorHits > 0 && <p className="xnote">{hiddenFloorHits} more — keep typing to narrow down.</p>}
        </>
      )}
    </aside>
  )
}

function toItem(x: { id: string; name: string }): TreeItem {
  return { id: x.id, name: x.name }
}
