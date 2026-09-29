import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { PlusIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SearchIcon } from '@/components/icons'
import { ICONS } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '@/features/space-management/components/actionIcons'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { Can } from '@/features/auth/components/Can'
import { HierarchyViewers, Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { HighlightedText } from '@/features/space-management/components/HighlightedText'
import { Pager } from '@/components/Pager'
import { AddNodeModal } from '@/features/space-management/components/AddNodeModal'
import type { ModalState } from '@/features/space-management/components/AddNodeModal'
import { EditDetailsModal } from '@/features/space-management/components/EditDetailsModal'
import type { EditDetailsState } from '@/features/space-management/components/EditDetailsModal'
import { useToast } from '@/components/Toast'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'
import { useConfirm } from '@/components/ConfirmDialog'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useListParams } from '@/hooks/useListParams'
import { notifyHierarchyChanged } from '@/features/space-management/hierarchyEvents'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { ApiError, getBuildings, deleteBuilding, getBuildingDeleteImpact, restoreBuilding } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

export function BuildingsListPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  // The rules page saves with Edit and changes closures with the Overrides permissions; with
  // none of them it opens read-only, so the menu says so.
  const canEditRules = usePermission([Permissions.Buildings.Edit, Permissions.Overrides.Create, Permissions.Overrides.Delete])
  // A building opens into its floors — a link only for someone who may see them, otherwise
  // the name is just a name rather than a click that ends on "you can't see this".
  const canOpenFloors = usePermission(HierarchyViewers.Floors)
  const navigate = useNavigate()

  const list = useListParams()
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  const { confirm, dialog: confirmDialog } = useConfirm()

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.hierarchy.buildings({ search: list.search, showDeleted: list.showDeleted, page: list.page, pageSize: list.pageSize }),
    async () => {
      const buildingsResult = await getBuildings(token, {
        filter: list.search || undefined,
        includeDeleted: list.showDeleted,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      })
      return { buildings: buildingsResult.items, totalCount: buildingsResult.totalCount }
    },
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
      confirmAndRun(name, confirmMessage, remove, ` deleted.`)
      return
    }
    if ((await askImpact({ mode: 'delete', impact: affected, subject: name })) !== 'cancel') return
    runAction(
      remove,
      affected.count > 0 ? `${name} deleted · ${affected.count} ${affected.count === 1 ? 'booking' : 'bookings'} cancelled.` : `${name} deleted.`,
    )
  }

  async function confirmAndRun(name: string, confirmMessage: string, action: () => Promise<unknown>, successMessage: string) {
    if (!(await confirm({ title: `Delete “${name}”?`, description: confirmMessage, confirmLabel: 'Delete', destructive: true }))) return
    runAction(action, successMessage)
  }

  return (
    <>
      <div className="main">
        <div className="content">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="pagetitle">Space management</h1>
              <p className="lead">
                {status === 'success' ? `${data.totalCount} building${data.totalCount === 1 ? '' : 's'}. ` : ''}
                Open a building to manage its floors and spaces.
              </p>
            </div>
            <Can permission={Permissions.Buildings.Create}>
              <Button onClick={() => setModal({ kind: 'building' })}>
                <PlusIcon /> Add building
              </Button>
            </Can>
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
                      {canOpenFloors ? (
                        <Link to={`/admin/buildings/${building.id}/floors`} className="lbl2">
                          <HighlightedText text={building.name} query={list.search} />
                        </Link>
                      ) : (
                        <span className="lbl2">
                          <HighlightedText text={building.name} query={list.search} />
                        </span>
                      )}
                      {building.buildingNumber && <span className="m">{building.buildingNumber}</span>}
                      {building.isDeleted && <span className="badge cancelled">Deleted</span>}
                      <span className="actions">
                        {building.isDeleted ? (
                          <Can permission={Permissions.Buildings.Edit}>
                            <button
                              className="rowbtn"
                              title={`Restore ${building.name}`}
                              aria-label={`Restore ${building.name}`}
                              onClick={() => runAction(() => restoreBuilding(token, building.id), `${building.name} restored.`)}
                            >
                              <RestoreIcon />
                            </button>
                          </Can>
                        ) : (
                          <RowActionsMenu
                            label={building.name}
                            actions={[
                              {
                                label: 'Edit details',
                                permission: Permissions.Buildings.Edit,
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
                                label: canEditRules ? 'Edit constraints' : 'View constraints',
                                permission: Permissions.Buildings.Default,
                                icon: <PencilIcon />,
                                onClick: () => navigate(`/admin/constraints/building/${building.id}`),
                              },
                              {
                                label: 'Delete',
                                permission: Permissions.Buildings.Delete,
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
      {confirmDialog}
    </>
  )
}
