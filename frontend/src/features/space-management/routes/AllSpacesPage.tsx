import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { ICONS, iconKeyToIconName } from '../components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '../components/actionIcons'
import { RowActionsMenu } from '../components/RowActionsMenu'
import { HighlightedText } from '../components/HighlightedText'
import { Pager } from '../../../components/Pager'
import { EditDetailsModal } from '../components/EditDetailsModal'
import type { EditDetailsState } from '../components/EditDetailsModal'
import { ManageSpaceTypesModal } from '../components/ManageSpaceTypesModal'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useListParams } from '../../../hooks/useListParams'
import { BuildingPicker } from '../components/BuildingPicker'
import { ApiError, getSpaces, getSpaceTypes, deleteSpace, restoreSpace } from '../api/spaceManagementApi'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'
import '../../../styles/login.css'

const ALL_SPACE_TYPES = ''

// Standalone, unscoped counterpart to SpacesListPage.tsx (which lists one floor's spaces) —
// this lists every space across every floor/building in one flat, paged table, with the
// parent Floor's and Building's names shown per row. Reached directly from the sidebar. No
// "+ Space" here: creation stays on the scoped drill-down page, reached by opening a
// specific floor.
export function AllSpacesPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const navigate = useNavigate()

  const list = useListParams()
  const spaceTypeId = list.getFilter('type')
  const buildingId = list.getFilter('building')
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const [manageTypesOpen, setManageTypesOpen] = useState(false)
  const { toast, showToast } = useToast()

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const [spacesResult, spaceTypesResult] = await Promise.all([
        getSpaces(token, {
          filter: list.search || undefined,
          spaceTypeId: spaceTypeId || undefined,
          buildingId: buildingId || undefined,
          includeDeleted: list.showDeleted,
          skipCount: list.page * list.pageSize,
          maxResultCount: list.pageSize,
        }),
        getSpaceTypes(token),
      ])
      return { spaces: spacesResult.items, totalCount: spacesResult.totalCount, spaceTypes: spaceTypesResult.items }
    },
    [token, list.search, spaceTypeId, buildingId, list.showDeleted, list.page, list.pageSize],
    { keepPreviousData: true },
  )

  const spaceTypeById = useMemo(() => {
    const map = new Map<string, { name: string; iconKey: number }>()
    for (const st of data?.spaceTypes ?? []) map.set(st.id, st)
    return map
  }, [data])

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
            <h1 className="pagetitle">Spaces</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} space${data.totalCount === 1 ? '' : 's'} across every floor. ` : ''}
              Bookable spaces across the whole portfolio.
            </p>
          </div>

          <section className="card" id="spaces">
            <div className="cardhead">
              <h2 className="sectiontitle">All spaces</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search spaces, floors or buildings…"
                    autoComplete="off"
                    aria-label="Search spaces, floors or buildings"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <BuildingPicker
                  token={token}
                  value={buildingId}
                  noneLabel="All buildings"
                  ariaLabel="Filter by building"
                  onChange={(id) => list.setFilter('building', id)}
                />
                <select
                  className="ctrl"
                  aria-label="Filter by space type"
                  value={spaceTypeId}
                  onChange={(e) => list.setFilter('type', e.target.value)}
                >
                  <option value={ALL_SPACE_TYPES}>All types</option>
                  {(data?.spaceTypes ?? []).map((st) => (
                    <option key={st.id} value={st.id}>
                      {st.name}
                    </option>
                  ))}
                </select>
                <label className="chk">
                  <input
                    type="checkbox"
                    checked={list.showDeleted}
                    onChange={(e) => list.setShowDeleted(e.target.checked)}
                  />
                  Show deleted
                </label>
                <button className="btn sm sec" onClick={() => setManageTypesOpen(true)}>
                  Manage space types
                </button>
              </div>
            </div>

            <p className="treelegend">
              <span>{ICONS['meeting-room']} Meeting room</span>
              <span>{ICONS['focus-pod']} Focus pod</span>
              <span>{ICONS.desk} Desk</span>
            </p>

            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <p className="treeempty">Loading spaces…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load spaces: {error.message}</p>}

              {status === 'success' && data.spaces.length === 0 && (
                <p className="treeempty">
                  {list.search || spaceTypeId || buildingId ? 'Nothing matches the current filters.' : 'No spaces yet.'}
                </p>
              )}

              {status === 'success' &&
                data.spaces.map((space) => (
                  <div className="node l1" data-level="space" key={space.id} style={space.isDeleted ? { opacity: 0.55 } : undefined}>
                    <span className="spaceicon">{ICONS[iconKeyToIconName(spaceTypeById.get(space.spaceTypeId)?.iconKey ?? 3)]}</span>
                    <span className="lbl2">
                      <HighlightedText text={space.name} query={list.search} />
                    </span>
                    {spaceTypeById.get(space.spaceTypeId)?.name && (
                      <span className="badge type">{spaceTypeById.get(space.spaceTypeId)?.name}</span>
                    )}
                    <span className="m">
                      <HighlightedText text={space.buildingName ?? ''} query={list.search} /> ·{' '}
                      <HighlightedText text={space.floorName ?? ''} query={list.search} />
                    </span>
                    <span className="m">
                      {space.capacity} seat{space.capacity === 1 ? '' : 's'}
                    </span>
                    {space.hasOverrides && <span className="badge completed">Custom</span>}
                    {space.isDeleted && <span className="badge cancelled">Deleted</span>}
                    <span className="actions">
                      {space.isDeleted ? (
                        <button
                          className="rowbtn"
                          title={`Restore ${space.name}`}
                          aria-label={`Restore ${space.name}`}
                          onClick={() => runAction(() => restoreSpace(token, space.id), `${space.name} restored.`)}
                        >
                          <RestoreIcon />
                        </button>
                      ) : (
                        <RowActionsMenu
                          label={space.name}
                          actions={[
                            {
                              label: 'Edit details',
                              icon: <DetailsIcon />,
                              onClick: () =>
                                setEditState({
                                  kind: 'space',
                                  id: space.id,
                                  name: space.name,
                                  spaceTypeId: space.spaceTypeId,
                                  capacity: space.capacity,
                                }),
                            },
                            {
                              label: 'Edit constraints',
                              icon: <PencilIcon />,
                              onClick: () => navigate(`/admin/constraints/space/${space.id}`),
                            },
                            {
                              label: 'Delete',
                              icon: <TrashIcon />,
                              destructive: true,
                              onClick: () => confirmAndRun(`Delete "${space.name}"?`, () => deleteSpace(token, space.id), `${space.name} deleted.`),
                            },
                          ]}
                        />
                      )}
                    </span>
                  </div>
                ))}
            </div>

            {status === 'success' && (
              <Pager
                page={list.page}
                pageSize={list.pageSize}
                totalCount={data.totalCount}
                onPageChange={list.setPage}
                onPageSizeChange={list.setPageSize}
              />
            )}
          </section>
        </div>
      </div>

      {editState && (
        <EditDetailsModal
          state={editState}
          token={token}
          spaceTypes={data?.spaceTypes ?? []}
          onClose={() => setEditState(null)}
          onSaved={() => {
            showToast('Details saved.')
            refetch()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
      {manageTypesOpen && (
        <ManageSpaceTypesModal
          token={token}
          spaceTypes={data?.spaceTypes ?? []}
          onClose={() => setManageTypesOpen(false)}
          onChanged={refetch}
          onError={(message) => showToast(message, 'error')}
          onSuccess={(message) => showToast(message)}
        />
      )}
      <Toast toast={toast} />
    </div>
  )
}
