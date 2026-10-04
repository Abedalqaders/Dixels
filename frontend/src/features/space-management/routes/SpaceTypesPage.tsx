import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { PencilIcon, PlusIcon, SearchIcon, Trash2Icon } from 'lucide-react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Sidebar } from '@/components/Sidebar'
import { EmptyState, NoResults } from '@/components/EmptyState'
import { TagIcon } from '@/components/icons'
import { TablePagination } from '@/components/TablePagination'
import { useToast } from '@/components/Toast'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useListParams } from '@/hooks/useListParams'
import { ApiError, deleteSpaceType, getSpaceTypes } from '@/features/space-management/api/spaceManagementApi'
import type { SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { SpaceTypeFormDialog } from '@/features/space-management/components/SpaceTypeFormDialog'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { Can } from '@/features/auth/components/Can'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { ICON_OPTIONS, ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TableSkeleton } from '@/components/LoadingSkeletons'
import { TopBar } from '@/components/TopBar'

// Space types aren't part of the Building → Floor hierarchy, so this page sits outside
// SpaceManagementLayout — no explorer tree beside it, just the app nav.
//
// Search and paging run in the database, like every other admin list: a name in any language
// matches (an admin may search in a language other than the screen's), and the page comes
// sorted by the name shown. Page/size/search live in the URL (useListParams).
//
// Each row shows the name in the reader's language (the server picks it), and a muted note
// naming the languages the type has no name in yet — those readers see the default one.
export function SpaceTypesPage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { showToast } = useToast()
  const list = useListParams()
  const canAdd = usePermission(Permissions.SpaceTypes.Create)

  // undefined = closed, null = adding, a type = editing that type.
  const [editing, setEditing] = useState<SpaceTypeDto | null | undefined>(undefined)
  const [deleting, setDeleting] = useState<SpaceTypeDto | null>(null)
  const [deleteBusy, setDeleteBusy] = useState(false)

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.spaceTypes.list({ search: list.search, page: list.page, pageSize: list.pageSize }),
    () =>
      getSpaceTypes(token, {
        filter: list.search || undefined,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      }),
    { keepPreviousData: true },
  )

  const pageRows = data?.items ?? []

  function handleSaved(message: string) {
    setEditing(undefined)
    showToast(message)
    refetch()
  }

  async function handleDelete() {
    if (!deleting) return
    setDeleteBusy(true)
    try {
      await deleteSpaceType(token, deleting.id)
      showToast(t('SpaceTypes:Deleted', { name: deleting.name }))
      refetch()
    } catch (err) {
      // Surfaces the backend's real message, e.g. the SpaceTypeInUse block, rather than a
      // generic failure — the admin needs to know *why* before they can act on it.
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    } finally {
      setDeleteBusy(false)
      setDeleting(null)
    }
  }

  // What an empty list and a search that found nothing both offer. Without the permission
  // there's no Add, so a search that found nothing falls back to Clear filters.
  const addAction = canAdd ? (
    <Button variant="outline" size="sm" onClick={() => setEditing(null)}>
      <PlusIcon />
      {t('SpaceTypes:Add')}
    </Button>
  ) : undefined

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <TopBar />
        <div className="content">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="pagetitle">{t('SpaceTypes:Title')}</h1>
              <p className="lead">{t('SpaceTypes:Lead')}</p>
            </div>
            <Can permission={Permissions.SpaceTypes.Create}>
              <Button onClick={() => setEditing(null)}>
                <PlusIcon />
                {t('SpaceTypes:Add')}
              </Button>
            </Can>
          </div>

          <Card className="gap-0 rounded-2xl py-0 shadow-md">
            <CardHeader className="flex flex-wrap items-center gap-3 border-b px-5 py-4 [.border-b]:pb-4">
              <CardAction className="flex w-full flex-wrap items-center gap-2 sm:w-auto">
                <div className="relative w-full sm:w-auto">
                  <SearchIcon className="pointer-events-none absolute top-1/2 inset-s-3 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="w-full rounded-full sm:w-56 border-transparent bg-muted/40 ps-9 focus-visible:border-ring focus-visible:bg-background"
                    placeholder={t('SpaceTypes:Search')}
                    aria-label={t('SpaceTypes:SearchLabel')}
                    autoComplete="off"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
              </CardAction>
            </CardHeader>

            <CardContent className={`px-0${isRefreshing ? ' opacity-55 transition-opacity' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TableSkeleton label={t('SpaceTypes:Loading')} columns={3} />}
              {status === 'error' && <p className="treeempty">{t('SpaceTypes:LoadFailed', { error: error.message })}</p>}
              {status === 'success' && pageRows.length === 0 &&
                (list.search ? (
                  <NoResults onClear={() => list.clearFilters()} action={addAction} />
                ) : (
                  <EmptyState icon={<TagIcon />} title={t('SpaceTypes:Empty')} description={t('SpaceTypes:EmptyHint')} action={addAction} />
                ))}

              {status === 'success' && pageRows.length > 0 && (
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/40 hover:bg-muted/40">
                      <TableHead className="w-16 ps-4 font-semibold">{t('SpaceTypes:ColumnIcon')}</TableHead>
                      <TableHead className="font-semibold">{t('SpaceTypes:ColumnName')}</TableHead>
                      <TableHead className="hidden font-semibold sm:table-cell">{t('SpaceTypes:ColumnIconStyle')}</TableHead>
                      <TableHead className="w-16 pe-4 text-end"><span className="sr-only">{t('Common:Actions')}</span></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {pageRows.map((st) => (
                      <TableRow key={st.id}>
                        <TableCell className="py-3 ps-4">
                          <span className="grid size-9 place-items-center rounded-md bg-accent text-accent-foreground [&_svg]:size-5">
                            {ICONS[iconKeyToIconName(st.iconKey)]}
                          </span>
                        </TableCell>
                        <TableCell className="py-3 font-medium">{st.name}</TableCell>
                        <TableCell className="hidden py-3 text-muted-foreground sm:table-cell">
                          {t(ICON_OPTIONS.find((o) => o.value === st.iconKey)?.labelKey ?? 'Enum:IconKey.Generic')}
                        </TableCell>
                        <TableCell className="py-3 pe-4 text-end">
                          <RowActionsMenu
                            label={st.name}
                            actions={[
                              { label: t('Common:Edit'), permission: Permissions.SpaceTypes.Edit, icon: <PencilIcon />, onClick: () => setEditing(st) },
                              {
                                label: t('Common:Delete'),
                                permission: Permissions.SpaceTypes.Delete,
                                icon: <Trash2Icon />,
                                onClick: () => setDeleting(st),
                                destructive: true,
                              },
                            ]}
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>

            {status === 'success' && (
              <div className="border-t">
                <TablePagination
                  page={list.page}
                  pageSize={list.pageSize}
                  totalCount={data.totalCount}
                  onPageChange={list.setPage}
                  onPageSizeChange={list.setPageSize}
                />
              </div>
            )}
          </Card>
        </div>
      </div>

      {editing !== undefined && (
        <SpaceTypeFormDialog token={token} spaceType={editing} onClose={() => setEditing(undefined)} onSaved={handleSaved} />
      )}

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && !deleteBusy && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('SpaceTypes:DeleteTitle', { name: deleting?.name ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('SpaceTypes:DeleteDetail')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteBusy}>{t('Common:Cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={deleteBusy}
              onClick={(e) => {
                // Keep the dialog open until the request finishes; handleDelete closes it.
                e.preventDefault()
                void handleDelete()
              }}
            >
              {deleteBusy ? t('Common:Deleting') : t('Common:Delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

    </div>
  )
}
