import { useAuth } from 'react-oidc-context'
import { SearchIcon } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Sidebar } from '../../../components/Sidebar'
import { TablePagination } from '../../../components/TablePagination'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useListParams } from '../../../hooks/useListParams'
import { ApiError, assignUserBuilding, buildingIdOf, getUserRoles, getUsers } from '../api/usersApi'
import type { IdentityUserDto } from '../api/usersApi'
import { getBuilding } from '../../space-management/api/spaceManagementApi'
import { BuildingPicker } from '../../space-management/components/BuildingPicker'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'

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
  roles: string[]
  buildingId: string | null
  buildingName: string | null
}

// Search, the building filter and paging all run on the server (ABP's GET /api/identity/users)
// — unlike space types, a company can have thousands of users. ABP's list doesn't carry
// roles or building names, so those are fetched per page: roles once per user, each
// building once however many users share it.
export function AdminUsersPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()

  const list = useListParams()
  const buildingFilter = list.getFilter('building')

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const usersResult = await getUsers(token, {
        filter: list.search || undefined,
        buildingId: buildingFilter || undefined,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      })

      const buildingIds = [...new Set(usersResult.items.map(buildingIdOf).filter((id): id is string => id !== null))]
      const [rolesPerUser, buildingNames] = await Promise.all([
        Promise.all(usersResult.items.map((u) => getUserRoles(token, u.id).then((r) => r.items.map((role) => role.name)))),
        Promise.all(
          // A building deleted since it was assigned reads back as "not found" — show it as
          // unassigned rather than failing the whole page.
          buildingIds.map((id) => getBuilding(token, id).then((b) => [id, b.name] as const, () => [id, null] as const)),
        ),
      ])
      const nameById = new Map(buildingNames)

      const rows: UserRow[] = usersResult.items.map((user, i) => {
        const buildingId = buildingIdOf(user)
        const buildingName = buildingId ? (nameById.get(buildingId) ?? null) : null
        return { user, roles: rolesPerUser[i], buildingId: buildingName ? buildingId : null, buildingName }
      })
      return { rows, totalCount: usersResult.totalCount }
    },
    [token, list.search, buildingFilter, list.page, list.pageSize],
    { keepPreviousData: true },
  )

  async function handleAssign(userId: string, userLabel: string, buildingId: string) {
    try {
      await assignUserBuilding(token, userId, buildingId === UNASSIGNED ? null : buildingId)
      showToast(buildingId === UNASSIGNED ? `${userLabel} unassigned.` : `${userLabel} assigned to a building.`)
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
              {status === 'success' ? `${data.totalCount} user${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Choose which single building each user can see — they never see any other.
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
              {status === 'loading' && <p className="treeempty">Loading users…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load users: {error.message}</p>}
              {status === 'success' && data.rows.length === 0 && (
                <p className="treeempty">
                  {list.search || buildingFilter ? 'No users match the current search and filter.' : 'No user accounts yet.'}
                </p>
              )}

              {status === 'success' && data.rows.length > 0 && (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="pl-4">User</TableHead>
                      <TableHead className="hidden md:table-cell">Email</TableHead>
                      <TableHead>Roles</TableHead>
                      <TableHead className="pr-4">Building</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.rows.map(({ user, roles, buildingId, buildingName }) => {
                      const label = labelFor(user)
                      return (
                        <TableRow key={user.id}>
                          <TableCell className="pl-4">
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
                          <TableCell>
                            <div className="flex flex-wrap gap-1">
                              {roles.length === 0 && <span className="text-muted-foreground">—</span>}
                              {roles.map((role) => (
                                <Badge key={role} variant="outline" className="capitalize">
                                  {role}
                                </Badge>
                              ))}
                            </div>
                          </TableCell>
                          <TableCell className="pr-4">
                            <BuildingPicker
                              token={token}
                              value={buildingId ?? UNASSIGNED}
                              selectedName={buildingName}
                              noneLabel="Not assigned"
                              ariaLabel={`Building for ${label}`}
                              onChange={(id) => handleAssign(user.id, label, id)}
                            />
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

      <Toast toast={toast} />
    </div>
  )
}
