import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { PlusIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SearchIcon } from '@/components/icons'
import { EmptyState, NoResults } from '@/components/EmptyState'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon } from '@/features/space-management/components/actionIcons'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { Can } from '@/features/auth/components/Can'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { SpaceTypeFilter } from '@/features/space-management/components/SpaceTypeFilter'
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
import type { ReservationImpactDto } from '@/lib/api/reservationImpact'
import { ApiError, getBuilding, getFloor, getSpaces, getSpaceTypes, deleteSpace, getSpaceDeleteImpact } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'
import { TopBar } from '@/components/TopBar'

export function SpacesListPage() {
  const { buildingId = '', floorId = '' } = useParams()
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const canEditRules = useCanEditRules('space')
  const canAdd = usePermission(Permissions.Spaces.Create)
  const navigate = useNavigate()

  const list = useListParams()
  const spaceTypeId = list.getFilter('type')
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  const { confirm, dialog: confirmDialog } = useConfirm()

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.hierarchy.spaces(floorId, { search: list.search, spaceTypeId, page: list.page, pageSize: list.pageSize }),
    async () => {
      // The building only for its name in the breadcrumbs: a single floor comes back without it.
      const [building, floor, spaceTypesResult, spacesResult] = await Promise.all([
        getBuilding(token, buildingId),
        getFloor(token, floorId),
        getSpaceTypes(token),
        getSpaces(token, {
          floorId,
          filter: list.search || undefined,
          spaceTypeId: spaceTypeId || undefined,
          skipCount: list.page * list.pageSize,
          maxResultCount: list.pageSize,
        }),
      ])
      return { building, floor, spaceTypes: spaceTypesResult.items, spaces: spacesResult.items, totalCount: spacesResult.totalCount }
    },
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
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    }
  }

  // A delete also cancels the upcoming bookings in what's deleted: when there are any, say
  // which (and whose) before going ahead; otherwise the plain confirm is enough.
  async function confirmDelete(name: string, confirmMessage: string, impact: () => Promise<ReservationImpactDto>, remove: () => Promise<unknown>) {
    let affected: ReservationImpactDto
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
      <Button variant="outline" size="sm" onClick={() => setModal({ kind: 'space', parentId: floorId, parentName: data.floor.name })}>
        <PlusIcon /> {t('Hierarchy:AddSpace')}
      </Button>
    ) : undefined

  return (
    <>
      <div className="main">
        <TopBar
          crumbs={[
            { label: t('Nav:SpaceManagement') },
            { label: t('Nav:Hierarchy'), to: '/admin/buildings' },
            { label: status === 'success' ? data.building.name : undefined, to: `/admin/buildings/${buildingId}/floors` },
            { label: status === 'success' ? data.floor.name : undefined },
          ]}
        />
        <div className="content">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="pagetitle">{status === 'success' ? data.floor.name : t('Hierarchy:Spaces')}</h1>
              <p className="lead">
                {status === 'success' ? `${t('Hierarchy:SpaceCount', { count: data.totalCount })} ` : ''}
                {t('Hierarchy:SpacesLead')}
              </p>
            </div>
            <Can permission={Permissions.Spaces.Create}>
              <Button
                disabled={status !== 'success'}
                onClick={() => status === 'success' && setModal({ kind: 'space', parentId: floorId, parentName: data.floor.name })}
              >
                <PlusIcon /> {t('Hierarchy:AddSpace')}
              </Button>
            </Can>
          </div>

          <section className="card" id="spaces">
            <div className="cardhead">
              <h2 className="sectiontitle">{t('Hierarchy:Spaces')}</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder={t('Hierarchy:SearchSpaces')}
                    autoComplete="off"
                    aria-label={t('Hierarchy:SearchSpacesLabel')}
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <SpaceTypeFilter spaceTypes={data?.spaceTypes ?? []} value={spaceTypeId} onChange={(id) => list.setFilter('type', id)} />
              </div>
            </div>


            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label={t('Hierarchy:LoadingSpaces')} />}
              {status === 'error' && <p className="treeempty">{t('Hierarchy:SpacesLoadFailed', { error: error.message })}</p>}

              {status === 'success' && data.spaces.length === 0 &&
                (list.search || spaceTypeId ? (
                  <NoResults onClear={() => list.clearFilters(['type'])} action={addAction} />
                ) : (
                  <EmptyState
                    icon={ICONS['meeting-room']}
                    title={t('Hierarchy:NoSpaces')}
                    description={t('Hierarchy:NoSpacesHint')}
                    action={addAction}
                  />
                ))}

              {status === 'success' &&
                data.spaces.map((space) => (
                  <div className="node l1" data-level="space" key={space.id}>
                    <span className="spaceicon">{ICONS[iconKeyToIconName(spaceTypeById.get(space.spaceTypeId)?.iconKey ?? 3)]}</span>
                    <span className="lbl2">
                      <HighlightedText text={space.name} query={list.search} />
                    </span>
                    {spaceTypeById.get(space.spaceTypeId)?.name && (
                      <span className="badge type">{spaceTypeById.get(space.spaceTypeId)?.name}</span>
                    )}
                    <span className="m">{t('Booking:Seats', { count: space.capacity })}</span>
                    {space.hasOverrides && <span className="badge completed">{t('Hierarchy:CustomBadge')}</span>}
                    <span className="actions">
                      <RowActionsMenu
                        label={space.name}
                        actions={[
                          {
                            label: t('Hierarchy:EditDetails'),
                            permission: Permissions.Spaces.Edit,
                            icon: <DetailsIcon />,
                            onClick: () =>
                              setEditState({
                                kind: 'space',
                                id: space.id,
                                names: space.names,
                                spaceTypeId: space.spaceTypeId,
                                capacity: space.capacity,
                              }),
                          },
                          {
                            label: canEditRules ? t('Hierarchy:EditConstraints') : t('Hierarchy:ViewConstraints'),
                            permission: Permissions.Spaces.Default,
                            icon: <PencilIcon />,
                            onClick: () => navigate(`/admin/constraints/space/${space.id}`),
                          },
                          {
                            label: t('Common:Delete'),
                            permission: Permissions.Spaces.Delete,
                            icon: <TrashIcon />,
                            destructive: true,
                            onClick: () =>
                              confirmDelete(
                                space.name,
                                t('Hierarchy:DeleteSpaceConfirm', { name: space.name }),
                                () => getSpaceDeleteImpact(token, space.id),
                                () => deleteSpace(token, space.id),
                              ),
                          },
                        ]}
                      />
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
            showToast(t('Hierarchy:SpaceAdded'))
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
            showToast(t('Hierarchy:DetailsSaved'))
            refetch()
          }}
          onError={(message) => showToast(message, 'error')}
        />
      )}
      {impactPrompt}
      {confirmDialog}
    </>
  )
}
