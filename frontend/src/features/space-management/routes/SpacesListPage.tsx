import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SearchIcon } from '@/components/icons'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '@/features/space-management/components/actionIcons'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { SpaceTypeFilter } from '@/features/space-management/components/SpaceTypeFilter'
import { HighlightedText } from '@/features/space-management/components/HighlightedText'
import { Pager } from '@/components/Pager'
import { AddNodeModal } from '@/features/space-management/components/AddNodeModal'
import type { ModalState } from '@/features/space-management/components/AddNodeModal'
import { EditDetailsModal } from '@/features/space-management/components/EditDetailsModal'
import type { EditDetailsState } from '@/features/space-management/components/EditDetailsModal'
import { Toast, useToast } from '@/components/Toast'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'
import { useAsync } from '@/hooks/useAsync'
import { useListParams } from '@/hooks/useListParams'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { ApiError, getFloor, getSpaces, getSpaceTypes, deleteSpace, getSpaceDeleteImpact, restoreSpace } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import '@/styles/login.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

export function SpacesListPage() {
  const { buildingId = '', floorId = '' } = useParams()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const navigate = useNavigate()

  const list = useListParams()
  const spaceTypeId = list.getFilter('type')
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { toast, showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const [floor, spaceTypesResult, spacesResult] = await Promise.all([
        getFloor(token, floorId),
        getSpaceTypes(token),
        getSpaces(token, {
          floorId,
          filter: list.search || undefined,
          spaceTypeId: spaceTypeId || undefined,
          includeDeleted: list.showDeleted,
          skipCount: list.page * list.pageSize,
          maxResultCount: list.pageSize,
        }),
      ])
      return { floor, spaceTypes: spaceTypesResult.items, spaces: spacesResult.items, totalCount: spacesResult.totalCount }
    },
    [token, floorId, list.search, spaceTypeId, list.showDeleted, list.page, list.pageSize],
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

  // A delete also cancels the upcoming bookings in what's deleted: when there are any, say
  // which (and whose) before going ahead; otherwise the plain confirm is enough.
  async function confirmDelete(name: string, confirmMessage: string, impact: () => Promise<BookingImpactDto>, remove: () => Promise<unknown>) {
    let affected: BookingImpactDto
    try {
      affected = await impact()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
      return
    }
    if (affected.count === 0) {
      confirmAndRun(confirmMessage, remove, `${name} deleted.`)
      return
    }
    if ((await askImpact({ mode: 'delete', impact: affected, subject: name })) !== 'cancel') return
    runAction(remove, `${name} deleted · ${affected.count} ${affected.count === 1 ? 'booking' : 'bookings'} cancelled.`)
  }

  function confirmAndRun(confirmMessage: string, action: () => Promise<unknown>, successMessage: string) {
    if (!window.confirm(confirmMessage)) return
    runAction(action, successMessage)
  }

  return (
    <>
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
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <SpaceTypeFilter spaceTypes={data?.spaceTypes ?? []} value={spaceTypeId} onChange={(id) => list.setFilter('type', id)} />
                <label className="chk">
                  <input
                    type="checkbox"
                    checked={list.showDeleted}
                    onChange={(e) => list.setShowDeleted(e.target.checked)}
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


            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label="Loading spaces…" />}
              {status === 'error' && <p className="treeempty">Couldn't load spaces: {error.message}</p>}

              {status === 'success' && data.spaces.length === 0 && (
                <p className="treeempty">
                  {list.search || spaceTypeId ? 'Nothing matches the current filters.' : 'No spaces yet — add one to get started.'}
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
                              onClick: () =>
                                confirmDelete(
                                  space.name,
                                  `Delete "${space.name}"?`,
                                  () => getSpaceDeleteImpact(token, space.id),
                                  () => deleteSpace(token, space.id),
                                ),
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
      {impactPrompt}
      <Toast toast={toast} />
    </>
  )
}
