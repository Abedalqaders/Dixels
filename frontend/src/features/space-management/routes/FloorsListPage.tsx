import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { PlusIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SearchIcon } from '@/components/icons'
import { EmptyState, NoResults } from '@/components/EmptyState'
import { ICONS } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon } from '@/features/space-management/components/actionIcons'
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
import { useCanEditRules } from '@/features/space-management/hooks/useCanEditRules'
import { useConfirm } from '@/components/ConfirmDialog'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useListParams } from '@/hooks/useListParams'
import { notifyHierarchyChanged } from '@/features/space-management/hierarchyEvents'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { ApiError, getBuilding, getFloors, deleteFloor, getFloorDeleteImpact } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'
import { TopBar } from '@/components/TopBar'

export function FloorsListPage() {
  const { buildingId = '' } = useParams()
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const canEditRules = useCanEditRules('floor')
  // A floor opens into its spaces — a link only for someone who may see them.
  const canOpenSpaces = usePermission(HierarchyViewers.Spaces)
  const canAdd = usePermission(Permissions.Floors.Create)
  const navigate = useNavigate()

  const list = useListParams()
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  const { confirm, dialog: confirmDialog } = useConfirm()

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.hierarchy.floors(buildingId, { search: list.search, page: list.page, pageSize: list.pageSize }),
    async () => {
      const [building, floorsResult] = await Promise.all([
        getBuilding(token, buildingId),
        getFloors(token, {
          buildingId,
          filter: list.search || undefined,
          skipCount: list.page * list.pageSize,
          maxResultCount: list.pageSize,
        }),
      ])
      return { building, floors: floorsResult.items, totalCount: floorsResult.totalCount }
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
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    }
  }

  // A delete also cancels the upcoming bookings in what's deleted: when there are any, say
  // which (and whose) before going ahead; otherwise the plain confirm is enough.
  async function confirmDelete(name: string, confirmMessage: string, impact: () => Promise<BookingImpactDto>, remove: () => Promise<unknown>) {
    let affected: BookingImpactDto
    try {
      affected = await impact()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
      return
    }
    if (affected.count === 0 && !affected.assignedEmployees) {
      confirmAndRun(name, confirmMessage, remove, t('Hierarchy:Deleted', { name }))
      return
    }
    if ((await askImpact({ mode: 'delete', impact: affected, subject: name })) !== 'cancel') return
    runAction(
      remove,
      affected.count > 0 ? t('Hierarchy:DeletedWithBookings', { name, count: affected.count }) : t('Hierarchy:Deleted', { name }),
    )
  }

  async function confirmAndRun(name: string, confirmMessage: string, action: () => Promise<unknown>, successMessage: string) {
    if (!(await confirm({ title: t('Hierarchy:DeleteTitle', { name }), description: confirmMessage, confirmLabel: t('Common:Delete'), destructive: true }))) return
    runAction(action, successMessage)
  }

  // What an empty list and a search that found nothing both offer. Without the permission
  // there's no Add, so a search that found nothing falls back to Clear filters.
  const addAction =
    canAdd && status === 'success' ? (
      <Button variant="outline" size="sm" onClick={() => setModal({ kind: 'floor', parentId: buildingId, parentName: data.building.name })}>
        <PlusIcon /> {t('Hierarchy:AddFloor')}
      </Button>
    ) : undefined

  return (
    <>
      <div className="main">
        <TopBar
          crumbs={[
            { label: t('Nav:SpaceManagement') },
            { label: t('Nav:Hierarchy'), to: '/admin/buildings' },
            { label: status === 'success' ? data.building.name : undefined },
          ]}
        />
        <div className="content">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="pagetitle">{status === 'success' ? data.building.name : t('Hierarchy:Floors')}</h1>
              <p className="lead">
                {status === 'success' ? `${t('Hierarchy:FloorCount', { count: data.totalCount })} ` : ''}
                {t('Hierarchy:FloorsLead')}
              </p>
            </div>
            <Can permission={Permissions.Floors.Create}>
              <Button
                disabled={status !== 'success'}
                onClick={() => status === 'success' && setModal({ kind: 'floor', parentId: buildingId, parentName: data.building.name })}
              >
                <PlusIcon /> {t('Hierarchy:AddFloor')}
              </Button>
            </Can>
          </div>

          <section className="card" id="floors">
            <div className="cardhead">
              <h2 className="sectiontitle">{t('Hierarchy:Floors')}</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder={t('Hierarchy:SearchFloors')}
                    autoComplete="off"
                    aria-label={t('Hierarchy:SearchFloorsLabel')}
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
              </div>
            </div>

            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label={t('Hierarchy:LoadingFloors')} />}
              {status === 'error' && <p className="treeempty">{t('Hierarchy:FloorsLoadFailed', { error: error.message })}</p>}

              {status === 'success' && data.floors.length === 0 &&
                (list.search ? (
                  <NoResults onClear={() => list.clearFilters()} action={addAction} />
                ) : (
                  <EmptyState
                    icon={ICONS.floor}
                    title={t('Hierarchy:NoFloors')}
                    description={t('Hierarchy:NoFloorsHint')}
                    action={addAction}
                  />
                ))}

              {status === 'success' &&
                data.floors.map((floor) => (
                  <div key={floor.id}>
                    <div className="node l1" data-level="floor">
                      {ICONS.floor}
                      {canOpenSpaces ? (
                        <Link to={`/admin/buildings/${buildingId}/floors/${floor.id}/spaces`} className="lbl2">
                          <HighlightedText text={floor.name} query={list.search} />
                        </Link>
                      ) : (
                        <span className="lbl2">
                          <HighlightedText text={floor.name} query={list.search} />
                        </span>
                      )}
                      {floor.floorNumber !== null && <span className="m">{t('Hierarchy:FloorNumberBadge', { number: floor.floorNumber })}</span>}
                      {floor.hasOverrides && <span className="badge completed">{t('Hierarchy:CustomBadge')}</span>}
                      <span className="actions">
                        <RowActionsMenu
                          label={floor.name}
                          actions={[
                            {
                              label: t('Hierarchy:EditDetails'),
                              permission: Permissions.Floors.Edit,
                              icon: <DetailsIcon />,
                              onClick: () =>
                                setEditState({ kind: 'floor', id: floor.id, names: floor.names, floorNumber: floor.floorNumber }),
                            },
                            {
                              label: canEditRules ? t('Hierarchy:EditConstraints') : t('Hierarchy:ViewConstraints'),
                              permission: Permissions.Floors.Default,
                              icon: <PencilIcon />,
                              onClick: () => navigate(`/admin/constraints/floor/${floor.id}`),
                            },
                            {
                              label: t('Common:Delete'),
                              permission: Permissions.Floors.Delete,
                              icon: <TrashIcon />,
                              destructive: true,
                              onClick: () =>
                                confirmDelete(
                                  floor.name,
                                  t('Hierarchy:DeleteFloorConfirm', { name: floor.name }),
                                  () => getFloorDeleteImpact(token, floor.id),
                                  () => deleteFloor(token, floor.id),
                                ),
                            },
                          ]}
                        />
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
            showToast(t('Hierarchy:FloorAdded'))
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
            showToast(t('Hierarchy:DetailsSaved'))
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
