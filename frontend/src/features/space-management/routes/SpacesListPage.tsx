import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { PlusIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SearchIcon } from '@/components/icons'
import { ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import { DetailsIcon, PencilIcon, TrashIcon, RestoreIcon } from '@/features/space-management/components/actionIcons'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { Can } from '@/features/auth/components/Can'
import { Permissions } from '@/features/auth/permissions/permissionNames'
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
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { ApiError, getFloor, getSpaces, getSpaceTypes, deleteSpace, getSpaceDeleteImpact, restoreSpace } from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TreeSkeleton } from '@/components/LoadingSkeletons'

export function SpacesListPage() {
  const { buildingId = '', floorId = '' } = useParams()
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const canEditRules = useCanEditRules('space')
  const navigate = useNavigate()

  const list = useListParams()
  const spaceTypeId = list.getFilter('type')
  const [modal, setModal] = useState<ModalState>(null)
  const [editState, setEditState] = useState<EditDetailsState>(null)
  const { showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  const { confirm, dialog: confirmDialog } = useConfirm()

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.hierarchy.spaces(floorId, { search: list.search, spaceTypeId, showDeleted: list.showDeleted, page: list.page, pageSize: list.pageSize }),
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

  return (
    <>
      <div className="main">
        <div className="content">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="breadcrumb">
                <Link to={`/admin/buildings/${buildingId}/floors`}>‹ {t('Hierarchy:Floors')}</Link>
              </p>
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
                <label className="chk">
                  <input
                    type="checkbox"
                    checked={list.showDeleted}
                    onChange={(e) => list.setShowDeleted(e.target.checked)}
                  />
                  {t('Hierarchy:ShowDeleted')}
                </label>
              </div>
            </div>


            <div className={`tree${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TreeSkeleton label={t('Hierarchy:LoadingSpaces')} />}
              {status === 'error' && <p className="treeempty">{t('Hierarchy:SpacesLoadFailed', { error: error.message })}</p>}

              {status === 'success' && data.spaces.length === 0 && (
                <p className="treeempty">
                  {list.search || spaceTypeId ? t('Hierarchy:NoFilterMatch') : t('Hierarchy:NoSpaces')}
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
                    <span className="m">{t('Booking:Seats', { count: space.capacity })}</span>
                    {space.hasOverrides && <span className="badge completed">{t('Hierarchy:CustomBadge')}</span>}
                    {space.isDeleted && <span className="badge cancelled">{t('Hierarchy:DeletedBadge')}</span>}
                    <span className="actions">
                      {space.isDeleted ? (
                        <Can permission={Permissions.Spaces.Edit}>
                          <button
                            className="rowbtn"
                            title={t('Hierarchy:Restore', { name: space.name })}
                            aria-label={t('Hierarchy:Restore', { name: space.name })}
                            onClick={() => runAction(() => restoreSpace(token, space.id), t('Hierarchy:Restored', { name: space.name }))}
                          >
                            <RestoreIcon />
                          </button>
                        </Can>
                      ) : (
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
