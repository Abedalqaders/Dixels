import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { ICONS } from '../components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '../components/actionIcons'
import { RowActionsMenu } from '../components/RowActionsMenu'
import { HighlightedText } from '../components/HighlightedText'
import { Pager } from '../../../components/Pager'
import { EditDetailsModal } from '../components/EditDetailsModal'
import type { EditDetailsState } from '../components/EditDetailsModal'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { ApiError, getBuildings, getFloors, deleteFloor, restoreFloor } from '../api/spaceManagementApi'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'
import '../../../styles/login.css'

const PAGE_SIZE = 10
const ALL_BUILDINGS = ''

// Standalone, unscoped counterpart to FloorsListPage.tsx (which lists one building's
// floors) — this lists every floor across every building in one flat, paged table, with the
// parent Building's name shown per row so it still reads as "which building is this in."
// Reached directly from the sidebar. No "+ Floor" here: creation stays on the scoped
// drill-down page, reached by opening a specific building.
export function AllFloorsPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const navigate = useNavigate()

  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search)
  const [buildingId, setBuildingId] = useState(ALL_BUILDINGS)
  const [showDeleted, setShowDeleted] = useState(false)
  const [page, setPage] = useState(0)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { toast, showToast } = useToast()

  const { status, data, error, refetch } = useAsync(async () => {
    const [floorsResult, buildingsResult] = await Promise.all([
      getFloors(token, {
        buildingId: buildingId || undefined,
        filter: debouncedSearch.trim() || undefined,
        includeDeleted: showDeleted,
        skipCount: page * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
      }),
      // maxResultCount is set high since this dropdown needs every building, not a page of
      // them — the endpoint defaults to ABP's standard page size (10) otherwise.
      getBuildings(token, { maxResultCount: 1000 }),
    ])
    return { floors: floorsResult.items, totalCount: floorsResult.totalCount, buildings: buildingsResult.items }
  }, [token, buildingId, debouncedSearch, showDeleted, page])

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(0)
  }

  function handleBuildingChange(value: string) {
    setBuildingId(value)
    setPage(0)
  }

  function handleShowDeletedChange(value: boolean) {
    setShowDeleted(value)
    setPage(0)
  }

  async function runAction(action: () => Promise<unknown>, successMessage: string) {
    try {
      await action()
      showToast(successMessage)
      refetch()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
    }
  }

  function confirmAndRun(confirmMessage: string, action: () => Promise<unknown>, successMessage: string) {
    if (!window.confirm(confirmMessage)) return
    runAction(action, successMessage)
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Floors</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} floor${data.totalCount === 1 ? '' : 's'} across every building. ` : ''}
              Open a floor to manage its spaces.
            </p>
          </div>

          <section className="card" id="floors">
            <div className="cardhead">
              <h2 className="sectiontitle">All floors</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search floors or buildings…"
                    autoComplete="off"
                    aria-label="Search floors or buildings"
                    value={search}
                    onChange={(e) => handleSearchChange(e.target.value)}
                  />
                </div>
                <select
                  className="ctrl"
                  aria-label="Filter by building"
                  value={buildingId}
                  onChange={(e) => handleBuildingChange(e.target.value)}
                >
                  <option value={ALL_BUILDINGS}>All buildings</option>
                  {(data?.buildings ?? []).map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </select>
                <label className="chk">
                  <input
                    type="checkbox"
                    checked={showDeleted}
                    onChange={(e) => handleShowDeletedChange(e.target.checked)}
                  />
                  Show deleted
                </label>
              </div>
            </div>

            <div className="tree">
              {status === 'loading' && <p className="treeempty">Loading floors…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load floors: {error.message}</p>}

              {status === 'success' && data.floors.length === 0 && (
                <p className="treeempty">
                  {debouncedSearch.trim() || buildingId ? 'Nothing matches the current filters.' : 'No floors yet.'}
                </p>
              )}

              {status === 'success' &&
                data.floors.map((floor) => (
                  <div key={floor.id} style={floor.isDeleted ? { opacity: 0.55 } : undefined}>
                    <div className="node l1" data-level="floor">
                      {ICONS.floor}
                      <Link to={`/admin/buildings/${floor.buildingId}/floors/${floor.id}/spaces`} className="lbl2">
                        <HighlightedText text={floor.name} query={debouncedSearch.trim()} />
                      </Link>
                      <span className="m">
                        <HighlightedText text={floor.buildingName ?? ''} query={debouncedSearch.trim()} />
                      </span>
                      {floor.floorNumber !== null && <span className="m">Floor {floor.floorNumber}</span>}
                      {floor.hasOverrides && <span className="badge completed">Custom</span>}
                      {floor.isDeleted && <span className="badge cancelled">Deleted</span>}
                      <span className="actions">
                        {floor.isDeleted ? (
                          <button
                            className="rowbtn"
                            title={`Restore ${floor.name}`}
                            aria-label={`Restore ${floor.name}`}
                            onClick={() => runAction(() => restoreFloor(token, floor.id), `${floor.name} restored.`)}
                          >
                            <RestoreIcon />
                          </button>
                        ) : (
                          <RowActionsMenu
                            label={floor.name}
                            actions={[
                              {
                                label: 'Edit details',
                                icon: <DetailsIcon />,
                                onClick: () =>
                                  setEditState({ kind: 'floor', id: floor.id, name: floor.name, floorNumber: floor.floorNumber }),
                              },
                              {
                                label: 'Edit constraints',
                                icon: <PencilIcon />,
                                onClick: () => navigate(`/admin/constraints/floor/${floor.id}`),
                              },
                              {
                                label: 'Delete',
                                icon: <TrashIcon />,
                                destructive: true,
                                onClick: () =>
                                  confirmAndRun(
                                    `Delete "${floor.name}"? This also deletes its spaces — they can all be restored together later.`,
                                    () => deleteFloor(token, floor.id),
                                    `${floor.name} deleted.`,
                                  ),
                              },
                            ]}
                          />
                        )}
                      </span>
                    </div>
                  </div>
                ))}
            </div>

            {status === 'success' && (
              <Pager page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />
            )}
          </section>
        </div>
      </div>

      {editState && (
        <EditDetailsModal
          state={editState}
          token={token}
          spaceTypes={[]}
          onClose={() => setEditState(null)}
          onSaved={() => {
            showToast('Details saved.')
            refetch()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
      <Toast toast={toast} />
    </div>
  )
}
