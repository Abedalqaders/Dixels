import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { ICONS, iconKeyToIconName } from '../components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '../components/actionIcons'
import { RowActionsMenu } from '../components/RowActionsMenu'
import { HighlightedText } from '../components/HighlightedText'
import { Pager } from '../../../components/Pager'
import { AddNodeModal } from '../components/AddNodeModal'
import type { ModalState } from '../components/AddNodeModal'
import { EditDetailsModal } from '../components/EditDetailsModal'
import type { EditDetailsState } from '../components/EditDetailsModal'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { ApiError, getFloor, getSpaces, getSpaceTypes, deleteSpace, restoreSpace } from '../api/spaceManagementApi'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'
import '../../../styles/login.css'

const PAGE_SIZE = 10
const ALL_SPACE_TYPES = ''

export function SpacesListPage() {
  const { buildingId = '', floorId = '' } = useParams()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const navigate = useNavigate()

  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search)
  const [spaceTypeId, setSpaceTypeId] = useState(ALL_SPACE_TYPES)
  const [showDeleted, setShowDeleted] = useState(false)
  const [page, setPage] = useState(0)
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { toast, showToast } = useToast()

  const { status, data, error, refetch } = useAsync(async () => {
    const [floor, spaceTypesResult, spacesResult] = await Promise.all([
      getFloor(token, floorId),
      getSpaceTypes(token),
      getSpaces(token, {
        floorId,
        filter: debouncedSearch.trim() || undefined,
        spaceTypeId: spaceTypeId || undefined,
        includeDeleted: showDeleted,
        skipCount: page * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
      }),
    ])
    return { floor, spaceTypes: spaceTypesResult.items, spaces: spacesResult.items, totalCount: spacesResult.totalCount }
  }, [token, floorId, debouncedSearch, spaceTypeId, showDeleted, page])

  const spaceTypeById = useMemo(() => {
    const map = new Map<string, { name: string; iconKey: number }>()
    for (const st of data?.spaceTypes ?? []) map.set(st.id, st)
    return map
  }, [data])

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(0)
  }

  function handleSpaceTypeChange(value: string) {
    setSpaceTypeId(value)
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
            <p className="breadcrumb">
              <Link to={`/admin/buildings/${buildingId}/floors`}>‹ Floors</Link>
            </p>
            <h1 className="pagetitle">{status === 'success' ? data.floor.name : 'Spaces'}</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} space${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Bookable spaces on this floor.
            </p>
          </div>

          <section className="card" id="spaces">
            <div className="cardhead">
              <h2 className="sectiontitle">Spaces</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search spaces…"
                    autoComplete="off"
                    aria-label="Search spaces"
                    value={search}
                    onChange={(e) => handleSearchChange(e.target.value)}
                  />
                </div>
                <select
                  className="ctrl"
                  aria-label="Filter by space type"
                  value={spaceTypeId}
                  onChange={(e) => handleSpaceTypeChange(e.target.value)}
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
                    checked={showDeleted}
                    onChange={(e) => handleShowDeletedChange(e.target.checked)}
                  />
                  Show deleted
                </label>
                <button
                  className="btn sm sec"
                  disabled={status !== 'success'}
                  onClick={() => status === 'success' && setModal({ kind: 'space', parentId: floorId, parentName: data.floor.name })}
                >
                  + Space
                </button>
              </div>
            </div>

            <p className="treelegend">
              <span>{ICONS['meeting-room']} Meeting room</span>
              <span>{ICONS['focus-pod']} Focus pod</span>
              <span>{ICONS.desk} Desk</span>
            </p>

            <div className="tree">
              {status === 'loading' && <p className="treeempty">Loading spaces…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load spaces: {error.message}</p>}

              {status === 'success' && data.spaces.length === 0 && (
                <p className="treeempty">
                  {debouncedSearch.trim() || spaceTypeId ? 'Nothing matches the current filters.' : 'No spaces yet — add one to get started.'}
                </p>
              )}

              {status === 'success' &&
                data.spaces.map((space) => (
                  <div className="node l1" data-level="space" key={space.id} style={space.isDeleted ? { opacity: 0.55 } : undefined}>
                    <span className="spaceicon">{ICONS[iconKeyToIconName(spaceTypeById.get(space.spaceTypeId)?.iconKey ?? 3)]}</span>
                    <span className="lbl2">
                      <HighlightedText text={space.name} query={debouncedSearch.trim()} />
                    </span>
                    {spaceTypeById.get(space.spaceTypeId)?.name && (
                      <span className="badge type">{spaceTypeById.get(space.spaceTypeId)?.name}</span>
                    )}
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
              <Pager page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />
            )}
          </section>
        </div>
      </div>

      {modal && (
        <AddNodeModal
          state={modal}
          token={token}
          spaceTypes={data?.spaceTypes ?? []}
          onClose={() => setModal(null)}
          onCreated={() => {
            showToast('Space added.')
            refetch()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
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
      <Toast toast={toast} />
    </div>
  )
}
