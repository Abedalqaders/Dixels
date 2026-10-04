import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, matchPath, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { SearchIcon } from '@/components/icons'
import { useDebouncedValue } from '@/hooks/useDebouncedValue'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { up } from '@/lib/breakpoints'
import { getBuilding, getBuildings, getFloors } from '@/features/space-management/api/spaceManagementApi'
import { useHierarchyChanged } from '@/features/space-management/hierarchyEvents'
import { HierarchyViewers } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { ICONS } from './spaceTypeIcons'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

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

// Points towards the end of the line when folded (right, or left in Arabic) and down when
// unfolded: the mirror comes first, then the stylesheet's 90° turn, which looks the same both ways.
function Chevron() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="rtl:-scale-x-100">
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

/** The admin's own choice (collapsed or not), or null when they never made one. */
function readCollapsed(): boolean | null {
  try {
    const stored = localStorage.getItem(COLLAPSED_KEY)
    return stored === '1' ? true : stored === '0' ? false : null
  } catch {
    return null
  }
}

function writeCollapsed(value: boolean) {
  try {
    localStorage.setItem(COLLAPSED_KEY, value ? '1' : '0')
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
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const location = useLocation()
  // Someone who may only see buildings gets a flat list: no unfolding into floors the API
  // would refuse, and a search that looks for buildings only.
  const canSeeFloors = usePermission(HierarchyViewers.Floors)

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

  // Without a choice of their own, the panel starts folded on phones (it would push the list
  // below the fold) and open from md up, where it sits beside the list.
  const roomForPanel = useMediaQuery(up('md'))
  const [chosenCollapsed, setCollapsed] = useState(readCollapsed)
  const collapsed = chosenCollapsed ?? !roomForPanel
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
    if (debouncedQuery && canSeeFloors) {
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
  }, [token, debouncedQuery, reloadKey, canSeeFloors])

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

  // Stable identity so the effects below can list it as a dependency honestly.
  const expand = useCallback(
    (id: string) => {
      if (!canSeeFloors) return
      setExpanded((e) => (e.has(id) ? e : new Set(e).add(id)))
    },
    [canSeeFloors],
  )

  // Opening a building's page from anywhere (a list row, a link, Back) unfolds it here too.
  useEffect(() => {
    if (activeBuildingId) expand(activeBuildingId)
  }, [activeBuildingId, expand])

  // A customer with a single building shouldn't have to click it open to see anything.
  const onlyBuildingId = !debouncedQuery && buildings.totalCount === 1 ? buildings.items[0]?.id : undefined
  useEffect(() => {
    if (onlyBuildingId) expand(onlyBuildingId)
  }, [onlyBuildingId, expand])

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
    if (!canSeeFloors) return
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
      <aside className="explorer collapsed" aria-label={t('Hierarchy:ExplorerLabel')}>
        <button
          type="button"
          className="xpanelbtn"
          aria-label={t('Hierarchy:ShowPanel')}
          title={t('Hierarchy:ShowPanel')}
          onClick={() => setPanelCollapsed(false)}
        >
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
    <aside className="explorer" aria-label={t('Hierarchy:ExplorerLabel')}>
      <div className="xhead">
        <div className="searchbox">
          <SearchIcon />
          <input
            type="text"
            placeholder={canSeeFloors ? t('Hierarchy:FindBuildingOrFloor') : t('Hierarchy:FindBuilding')}
            autoComplete="off"
            aria-label={canSeeFloors ? t('Hierarchy:FindBuildingOrFloorLabel') : t('Hierarchy:FindBuildingLabel')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>
        <button
          type="button"
          className="xpanelbtn"
          aria-label={t('Hierarchy:HidePanel')}
          title={t('Hierarchy:HidePanel')}
          onClick={() => setPanelCollapsed(true)}
        >
          <PanelIcon />
        </button>
      </div>

      <Link to={scopeLink()} className={`xrow root${rootIsCurrent ? ' on' : ''}`} aria-current={rootIsCurrent ? 'page' : undefined}>
        {ICONS.building}
        <span className="xlbl">{t('Hierarchy:AllBuildings')}</span>
        {!buildings.loading && !debouncedQuery && <span className="xcount">{buildings.totalCount}</span>}
      </Link>

      {debouncedQuery && canSeeFloors && <p className="xsection">{t('Hierarchy:Buildings')}</p>}
      <ul className="xtree" aria-busy={buildings.loading}>
        {visibleBuildings.map((building) => {
          const isOpen = expanded.has(building.id)
          const branch = floors[building.id]
          const isCurrent = building.id === activeBuildingId && !activeFloorId
          return (
            <li key={building.id}>
              <div className={`xrow${isCurrent ? ' on' : ''}`}>
                {canSeeFloors ? (
                  <button
                    type="button"
                    className={`xtoggle${isOpen ? ' open' : ''}`}
                    aria-expanded={isOpen}
                    aria-label={isOpen ? t('Hierarchy:Collapse', { name: building.name }) : t('Hierarchy:Expand', { name: building.name })}
                    onClick={() => toggle(building.id)}
                  >
                    <Chevron />
                  </button>
                ) : (
                  <span className="xtoggle" aria-hidden="true" />
                )}
                {canSeeFloors ? (
                  <Link to={scopeLink(building.id)} className="xlbl" title={building.name} aria-current={isCurrent ? 'page' : undefined}>
                    {building.name}
                  </Link>
                ) : (
                  <span className="xlbl" title={building.name}>
                    {building.name}
                  </span>
                )}
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
                  {branch?.loading && <li className="xnote">{t('Hierarchy:LoadingFloors')}</li>}
                  {branch?.error && (
                    <li className="xnote">
                      {t('Hierarchy:FloorsLoadFailedShort')}{' '}
                      <button type="button" className="xlink" onClick={() => fetchFloors(building.id, branch.items.length)}>
                        {t('Hierarchy:Retry')}
                      </button>
                    </li>
                  )}
                  {branch && !branch.loading && !branch.error && branch.items.length === 0 && <li className="xnote">{t('Hierarchy:NoFloorsYet')}</li>}
                  {branch && !branch.loading && branch.items.length < branch.totalCount && (
                    <li>
                      <button type="button" className="xmore" onClick={() => fetchFloors(building.id, branch.items.length)}>
                        {t('Hierarchy:ShowMoreFloors', { count: Math.min(FLOOR_PAGE, branch.totalCount - branch.items.length) })}
                      </button>
                    </li>
                  )}
                </ul>
              )}
            </li>
          )
        })}
      </ul>

      {buildings.loading && buildings.items.length === 0 && <TreeSkeleton label={t('Hierarchy:LoadingBuildings')} rows={4} />}
      {buildings.loading && buildings.items.length > 0 && <p className="xnote">{t('Hierarchy:Loading')}</p>}
      {buildings.error && (
        <p className="xnote">
          {t('Hierarchy:BuildingsLoadFailedShort')}{' '}
          <button type="button" className="xlink" onClick={() => setReloadKey((k) => k + 1)}>
            {t('Hierarchy:Retry')}
          </button>
        </p>
      )}
      {!buildings.loading && !buildings.error && buildings.items.length === 0 && (
        <p className="xnote">{debouncedQuery ? t('Hierarchy:NoBuildingsMatch') : t('Hierarchy:NoBuildingsYet')}</p>
      )}
      {!buildings.loading && hiddenBuildings > 0 && (
        <button type="button" className="xmore" onClick={loadMoreBuildings}>
          {t('Hierarchy:ShowMoreLeft', { left: hiddenBuildings })}
        </button>
      )}

      {debouncedQuery && canSeeFloors && (
        <>
          <p className="xsection">{t('Hierarchy:Floors')}</p>
          {floorHits === null && <p className="xnote">{t('Hierarchy:Searching')}</p>}
          {floorHits !== null && floorHits.items.length === 0 && <p className="xnote">{t('Hierarchy:NoFloorsMatch')}</p>}
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
          {hiddenFloorHits > 0 && <p className="xnote">{t('Hierarchy:MoreKeepTyping', { more: hiddenFloorHits })}</p>}
        </>
      )}
    </aside>
  )
}

function toItem(x: { id: string; name: string }): TreeItem {
  return { id: x.id, name: x.name }
}
