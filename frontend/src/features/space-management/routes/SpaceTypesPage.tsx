import { useMemo, useState } from 'react'
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
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Sidebar } from '@/components/Sidebar'
import { TablePagination } from '@/components/TablePagination'
import { Toast, useToast } from '@/components/Toast'
import { useAsync } from '@/hooks/useAsync'
import { useListParams } from '@/hooks/useListParams'
import { ApiError, deleteSpaceType, getSpaceTypes } from '@/features/space-management/api/spaceManagementApi'
import type { SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { SpaceTypeFormDialog } from '@/features/space-management/components/SpaceTypeFormDialog'
import { RowActionsMenu } from '@/features/space-management/components/RowActionsMenu'
import { Can } from '@/features/auth/components/Can'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { ICON_OPTIONS, ICONS, iconKeyToIconName } from '@/features/space-management/components/spaceTypeIcons'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TableSkeleton } from '@/components/LoadingSkeletons'

// Space types aren't part of the Building → Floor hierarchy, so this page sits outside
// SpaceManagementLayout — no explorer tree beside it, just the app nav.
//
// Search and paging run in the browser: the endpoint returns every type in one list (the
// space pickers need them all anyway), and a company has tens of types, not thousands.
// Page/size/search still live in the URL (useListParams), like every other admin list.
export function SpaceTypesPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()
  const list = useListParams()

  // undefined = closed, null = adding, a type = editing that type.
  const [editing, setEditing] = useState<SpaceTypeDto | null | undefined>(undefined)
  const [deleting, setDeleting] = useState<SpaceTypeDto | null>(null)
  const [deleteBusy, setDeleteBusy] = useState(false)

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => (await getSpaceTypes(token)).items,
    [token],
    { keepPreviousData: true },
  )

  const filtered = useMemo(() => {
    const term = list.search.toLowerCase()
    return (data ?? [])
      .filter((st) => !term || st.name.toLowerCase().includes(term))
      .sort((a, b) => a.name.localeCompare(b.name))
  }, [data, list.search])

  const pageRows = filtered.slice(list.page * list.pageSize, (list.page + 1) * list.pageSize)

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
      showToast(`${deleting.name} deleted.`)
      refetch()
    } catch (err) {
      // Surfaces the backend's real message, e.g. the SpaceTypeInUse block, rather than a
      // generic failure — the admin needs to know *why* before they can act on it.
      showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
    } finally {
      setDeleteBusy(false)
      setDeleting(null)
    }
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Space types</h1>
            <p className="lead">
              Shared across every building — renaming or re-icon-ing a type updates it everywhere it's used.
            </p>
          </div>

          <Card className="gap-0 rounded-2xl py-0 shadow-md">
            <CardHeader className="flex flex-wrap items-center gap-3 border-b px-5 py-4 [.border-b]:pb-4">
              <CardTitle>All space types</CardTitle>
              <CardAction className="flex items-center gap-2">
                <div className="relative">
                  <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="w-56 rounded-full border-transparent bg-muted/40 pl-9 focus-visible:border-ring focus-visible:bg-background"
                    placeholder="Search space types…"
                    aria-label="Search space types"
                    autoComplete="off"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <Can permission={Permissions.SpaceTypes.Create}>
                  <Button onClick={() => setEditing(null)}>
                    <PlusIcon />
                    Add type
                  </Button>
                </Can>
              </CardAction>
            </CardHeader>

            <CardContent className={`px-0${isRefreshing ? ' opacity-55 transition-opacity' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TableSkeleton label="Loading space types…" columns={3} />}
              {status === 'error' && <p className="treeempty">Couldn't load space types: {error.message}</p>}
              {status === 'success' && filtered.length === 0 && (
                <p className="treeempty">
                  {list.search ? 'No space types match the search.' : 'No space types yet — add one to get started.'}
                </p>
              )}

              {status === 'success' && filtered.length > 0 && (
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/40 hover:bg-muted/40">
                      <TableHead className="w-16 pl-4 font-semibold">Icon</TableHead>
                      <TableHead className="font-semibold">Name</TableHead>
                      <TableHead className="hidden font-semibold sm:table-cell">Icon style</TableHead>
                      <TableHead className="w-16 pr-4 text-right"><span className="sr-only">Actions</span></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {pageRows.map((st) => (
                      <TableRow key={st.id}>
                        <TableCell className="py-3 pl-4">
                          <span className="grid size-9 place-items-center rounded-md bg-accent text-accent-foreground [&_svg]:size-5">
                            {ICONS[iconKeyToIconName(st.iconKey)]}
                          </span>
                        </TableCell>
                        <TableCell className="py-3 font-medium">{st.name}</TableCell>
                        <TableCell className="hidden py-3 text-muted-foreground sm:table-cell">
                          {ICON_OPTIONS.find((o) => o.value === st.iconKey)?.label ?? 'Generic'}
                        </TableCell>
                        <TableCell className="py-3 pr-4 text-right">
                          <RowActionsMenu
                            label={st.name}
                            actions={[
                              { label: 'Edit', permission: Permissions.SpaceTypes.Edit, icon: <PencilIcon />, onClick: () => setEditing(st) },
                              {
                                label: 'Delete',
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
                  totalCount={filtered.length}
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
            <AlertDialogTitle>Delete {deleting?.name}?</AlertDialogTitle>
            <AlertDialogDescription>
              This can't be undone. A type that's still assigned to spaces can't be deleted.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteBusy}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={deleteBusy}
              onClick={(e) => {
                // Keep the dialog open until the request finishes; handleDelete closes it.
                e.preventDefault()
                void handleDelete()
              }}
            >
              {deleteBusy ? 'Deleting…' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <Toast toast={toast} />
    </div>
  )
}
