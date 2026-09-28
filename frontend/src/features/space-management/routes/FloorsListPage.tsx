import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SearchIcon } from '@/components/icons'
import { ICONS } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '@/features/space-management/components/actionIcons'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
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
import { notifyHierarchyChanged } from '@/features/space-management/hierarchyEvents'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { ApiError, getBuilding, getFloors, deleteFloor, getFloorDeleteImpact, restoreFloor } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import '@/styles/login.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

export function FloorsListPage() {
  const { buildingId = '' } = useParams()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const navigate = useNavigate()

  const list = useListParams()
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { toast, showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const [building, floorsResult] = await Promise.all([
        getBuilding(token, buildingId),
        getFloors(token, {
          buildingId,
          filter: list.search || undefined,
          includeDeleted: list.showDeleted,
          skipCount: list.page * list.pageSize,
          maxResultCount: list.pageSize,
        }),
      ])
      return { building, floors: floorsResult.items, totalCount: floorsResult.totalCount }
    },
    [token, buildingId, list.search, list.showDeleted, list.page, list.pageSize],
    { keepPreviousData: true },
  )

  async function runAction(action: () => Promise<unknown>, successMessage: string) {
    try {
      await action()
      showToast(successMessage)
      refetch()
      notifyHierarchyChanged()
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
    if (affected.count === 0 && !affected.assignedEmployees) {
      confirmAndRun(confirmMessage, remove, `${name} deleted.`)
      return
    }
    if ((await askImpact({ mode: 'delete', impact: affected, subject: name })) !== 'cancel') return
    runAction(
      remove,
      affected.count > 0 ? `${name} deleted · ${affected.count} ${affected.count === 1 ? 'booking' : 'bookings'} cancelled.` : `${name} deleted.`,
    )
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
              <Link to="/admin/buildings">‹ Buildings</Link>
            </p>
            <h1 className="pagetitle">{status === 'success' ? data.building.name : 'Floors'}</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} floor${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Open a floor to manage its spaces.
            </p>
          </div>

          <section className="card" id="floors">
            <div className="cardhead">
              <h2 className="sectiontitle">Floors</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search floors…"
                    autoComplete="off"
                    aria-label="Search floors"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
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
                  onClick={() =>
                    status === 'success' && setModal({ kind: 'floor', parentId: buildingId, parentName: data.building.name })
                  }
                >
                  + Floor
                </button>
              </div>
            </div>

            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label="Loading floors…" />}
              {status === 'error' && <p className="treeempty">Couldn't load floors: {error.message}</p>}

              {status === 'success' && data.floors.length === 0 && (
                <p className="treeempty">
                  {list.search ?'Nothing matches your search.' : 'No floors yet — add one to get started.'}
                </p>
              )}

              {status === 'success' &&
                data.floors.map((floor) => (
                  <div key={floor.id} style={floor.isDeleted ? { opacity: 0.55 } : undefined}>
                    <div className="node l1" data-level="floor">
                      {ICONS.floor}
                      <Link to={`/admin/buildings/${buildingId}/floors/${floor.id}/spaces`} className="lbl2">
                        <HighlightedText text={floor.name} query={list.search} />
                      </Link>
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
                                  confirmDelete(
                                    floor.name,
                                    `Delete "${floor.name}"? This also deletes its spaces — they can all be restored together later.`,
                                    () => getFloorDeleteImpact(token, floor.id),
                                    () => deleteFloor(token, floor.id),
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
          spaceTypes={[]}
          onClose={() => setModal(null)}
          onCreated={() => {
            showToast('Floor added.')
            refetch()
            notifyHierarchyChanged()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
      {editState && (
        <EditDetailsModal
          state={editState}
          token={token}
          spaceTypes={[]}
          onClose={() => setEditState(null)}
          onSaved={() => {
            showToast('Details saved.')
            refetch()
            notifyHierarchyChanged()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
      {impactPrompt}
      <Toast toast={toast} />
    </>
  )
}
