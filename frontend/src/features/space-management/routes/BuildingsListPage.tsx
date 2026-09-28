import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
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
import { ApiError, getBuildings, deleteBuilding, getBuildingDeleteImpact, restoreBuilding } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import '@/styles/login.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

export function BuildingsListPage() {
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
      const buildingsResult = await getBuildings(token, {
        filter: list.search || undefined,
        includeDeleted: list.showDeleted,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      })
      return { buildings: buildingsResult.items, totalCount: buildingsResult.totalCount }
    },
    [token, list.search, list.showDeleted, list.page, list.pageSize],
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
            <h1 className="pagetitle">Space management</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} building${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Open a building to manage its floors and spaces.
            </p>
          </div>

          <section className="card" id="buildings">
            <div className="cardhead">
              <h2 className="sectiontitle">Buildings</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search buildings…"
                    autoComplete="off"
                    aria-label="Search buildings"
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
                <button className="btn sm sec" onClick={() => setModal({ kind: 'building' })}>+ Building</button>
              </div>
            </div>

            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label="Loading buildings…" />}
              {status === 'error' && <p className="treeempty">Couldn't load buildings: {error.message}</p>}

              {status === 'success' && data.buildings.length === 0 && (
                <p className="treeempty">
                  {list.search ? 'Nothing matches your search.' : 'No buildings yet — add one to get started.'}
                </p>
              )}

              {status === 'success' &&
                data.buildings.map((building) => (
                  <div key={building.id} style={building.isDeleted ? { opacity: 0.55 } : undefined}>
                    <div className="node l1" data-level="building">
                      {ICONS.building}
                      <Link to={`/admin/buildings/${building.id}/floors`} className="lbl2">
                        <HighlightedText text={building.name} query={list.search} />
                      </Link>
                      {building.buildingNumber && <span className="m">{building.buildingNumber}</span>}
                      {building.isDeleted && <span className="badge cancelled">Deleted</span>}
                      <span className="actions">
                        {building.isDeleted ? (
                          <button
                            className="rowbtn"
                            title={`Restore ${building.name}`}
                            aria-label={`Restore ${building.name}`}
                            onClick={() => runAction(() => restoreBuilding(token, building.id), `${building.name} restored.`)}
                          >
                            <RestoreIcon />
                          </button>
                        ) : (
                          <RowActionsMenu
                            label={building.name}
                            actions={[
                              {
                                label: 'Edit details',
                                icon: <DetailsIcon />,
                                onClick: () =>
                                  setEditState({
                                    kind: 'building',
                                    id: building.id,
                                    name: building.name,
                                    buildingNumber: building.buildingNumber,
                                    timezone: building.timezone,
                                  }),
                              },
                              {
                                label: 'Edit constraints',
                                icon: <PencilIcon />,
                                onClick: () => navigate(`/admin/constraints/building/${building.id}`),
                              },
                              {
                                label: 'Delete',
                                icon: <TrashIcon />,
                                destructive: true,
                                onClick: () =>
                                  confirmDelete(
                                    building.name,
                                    `Delete "${building.name}"? This also deletes its floors and spaces — they can all be restored together later.`,
                                    () => getBuildingDeleteImpact(token, building.id),
                                    () => deleteBuilding(token, building.id),
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
            showToast('Building added.')
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
