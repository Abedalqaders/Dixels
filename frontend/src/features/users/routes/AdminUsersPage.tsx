import { useAuth } from 'react-oidc-context'
import { SearchIcon } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Sidebar } from '@/components/Sidebar'
import { TablePagination } from '@/components/TablePagination'
import { Toast, useToast } from '@/components/Toast'
import { useAsync } from '@/hooks/useAsync'
import { useListParams } from '@/hooks/useListParams'
import { ApiError, EMPLOYEE_ROLE, assignUserBuilding, buildingIdOf, getReassignImpact, getUsers } from '@/features/users/api/usersApi'
import type { IdentityUserDto } from '@/features/users/api/usersApi'
import { getBuilding, getBuildings } from '@/features/space-management/api/spaceManagementApi'
import { BuildingPicker } from '@/features/space-management/components/BuildingPicker'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TableSkeleton } from '@/components/LoadingSkeletons'

const UNASSIGNED = ''

function labelFor(user: IdentityUserDto): string {
  return [user.name, user.surname].filter(Boolean).join(' ') || user.userName
}

function initialsOf(label: string): string {
  return label
    .split(/[\s._-]+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

interface UserRow {
  user: IdentityUserDto
  buildingId: string | null
  buildingName: string | null
  /** The building they were assigned to was deleted: its name, so the admin can see who to reassign. */
  removedBuildingName: string | null
}

// Search, the building filter and paging all run on the server (ABP's GET /api/identity/users)
// — unlike space types, a company can have thousands of users. Only employees are listed:
// they're the ones who sit in a building; admins run the whole system. ABP's list doesn't
// carry building names, so each building is fetched once however many users share it.
export function AdminUsersPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()

  const list = useListParams()
  const buildingFilter = list.getFilter('building')

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const usersResult = await getUsers(token, {
        filter: list.search || undefined,
        buildingId: buildingFilter || undefined,
        role: EMPLOYEE_ROLE,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      })

      const buildingIds = [...new Set(usersResult.items.map(buildingIdOf).filter((id): id is string => id !== null))]
      const buildingNames = await Promise.all(
        // A building deleted since it was assigned reads back as "not found": the picker
        // shows them as not assigned, with the deleted building's name next to it.
        buildingIds.map((id) => getBuilding(token, id).then((b) => [id, b.name] as const, () => [id, null] as const)),
      )
      const nameById = new Map(buildingNames)

      // Names of those deleted buildings — one list call, only when there are any.
      const missing = buildingIds.filter((id) => !nameById.get(id))
      const removedNames = new Map<string, string>()
      if (missing.length > 0) {
        const all = await getBuildings(token, { includeDeleted: true, maxResultCount: 1000 }).catch(() => null)
        for (const b of all?.items ?? []) if (missing.includes(b.id)) removedNames.set(b.id, b.name)
      }

      const rows: UserRow[] = usersResult.items.map((user) => {
        const buildingId = buildingIdOf(user)
        const buildingName = buildingId ? (nameById.get(buildingId) ?? null) : null
        const removedBuildingName = buildingId && !buildingName ? (removedNames.get(buildingId) ?? 'A deleted building') : null
        return { user, buildingId: buildingName ? buildingId : null, buildingName, removedBuildingName }
      })
      return { rows, totalCount: usersResult.totalCount }
    },
    [token, list.search, buildingFilter, list.page, list.pageSize],
    { keepPreviousData: true },
  )

  async function handleAssign(userId: string, userLabel: string, buildingId: string) {
    try {
      // Upcoming bookings in the building they're leaving: keep or cancel, before moving them.
      const impact = await getReassignImpact(token, userId)
      let cancel = false
      if (impact.count > 0) {
        const choice = await askImpact({ mode: 'reassign', impact, subject: userLabel })
        if (!choice) return
        cancel = choice === 'cancel'
      }

      await assignUserBuilding(token, userId, buildingId === UNASSIGNED ? null : buildingId, cancel)
      const cancelled = cancel ? ` · ${impact.count} ${impact.count === 1 ? 'booking' : 'bookings'} cancelled` : ''
      showToast(buildingId === UNASSIGNED ? `${userLabel} unassigned${cancelled}.` : `${userLabel} assigned to a building${cancelled}.`)
      refetch()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
    }
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Users</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} employee${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Choose which single building each employee can see — they never see any other. Admins aren't listed: they manage every building.
            </p>
          </div>

          <Card className="gap-0 py-0">
            <CardHeader className="flex flex-wrap items-center gap-3 border-b px-4 py-3 [.border-b]:pb-3">
              <CardTitle>All users</CardTitle>
              <CardAction className="flex flex-wrap items-center gap-2">
                <div className="relative">
                  <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="w-64 pl-8"
                    placeholder="Search by name, username or email…"
                    aria-label="Search users"
                    autoComplete="off"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <BuildingPicker
                  token={token}
                  value={buildingFilter}
                  noneLabel="All buildings"
                  ariaLabel="Filter by building"
                  onChange={(id) => list.setFilter('building', id)}
                />
              </CardAction>
            </CardHeader>

            <CardContent className={`px-0${isRefreshing ? ' opacity-55 transition-opacity' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TableSkeleton label="Loading users…" columns={3} />}
              {status === 'error' && <p className="treeempty">Couldn't load users: {error.message}</p>}
              {status === 'success' && data.rows.length === 0 && (
                <p className="treeempty">
                  {list.search || buildingFilter ? 'No employees match the current search and filter.' : 'No employees yet.'}
                </p>
              )}

              {status === 'success' && data.rows.length > 0 && (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="pl-4">User</TableHead>
                      <TableHead className="hidden md:table-cell">Email</TableHead>
                      <TableHead className="pr-4 sm:w-60">Building</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.rows.map(({ user, buildingId, buildingName, removedBuildingName }) => {
                      const label = labelFor(user)
                      return (
                        <TableRow key={user.id}>
                          <TableCell className="py-2.5 pl-4">
                            <div className="flex items-center gap-3">
                              <Avatar>
                                <AvatarFallback className="text-xs font-semibold">{initialsOf(label)}</AvatarFallback>
                              </Avatar>
                              <div>
                                <div className="font-medium">{label}</div>
                                <div className="font-mono text-xs text-muted-foreground">{user.userName}</div>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell className="hidden text-muted-foreground md:table-cell">{user.email || '—'}</TableCell>
                          <TableCell className="pr-4">
                            <BuildingPicker
                              token={token}
                              value={buildingId ?? UNASSIGNED}
                              selectedName={buildingName}
                              noneLabel="Not assigned"
                              ariaLabel={`Building for ${label}`}
                              className="w-full sm:w-56"
                              onChange={(id) => handleAssign(user.id, label, id)}
                            />
                            {removedBuildingName && (
                              <p className="mt-1 text-xs text-[var(--state-expired-ink)]">
                                {removedBuildingName} (deleted) — pick another building
                              </p>
                            )}
                          </TableCell>
                        </TableRow>
                      )
                    })}
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

      {impactPrompt}
      <Toast toast={toast} />
    </div>
  )
}
