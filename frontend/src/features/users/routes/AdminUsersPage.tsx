import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { SearchIcon } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Card, CardAction, CardContent, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Sidebar } from '@/components/Sidebar'
import { TablePagination } from '@/components/TablePagination'
import { useToast } from '@/components/Toast'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useListParams } from '@/hooks/useListParams'
import {
  ApiError,
  assignUserBuilding,
  buildingIdOf,
  getReassignImpact,
  getRoleNames,
  getUserRoles,
  getUsers,
} from '@/features/users/api/usersApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { ADMIN_ROLE, roleLabel } from '@/features/auth/roles'
import type { IdentityUserDto } from '@/features/users/api/usersApi'
import { getBuilding, getBuildings } from '@/features/space-management/api/spaceManagementApi'
import { BuildingPicker } from '@/features/space-management/components/BuildingPicker'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { TableSkeleton } from '@/components/LoadingSkeletons'

const ALL_ROLES = 'all'

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
  /** The building they were assigned to was deleted, so the admin can see who to reassign. */
  buildingRemoved: boolean
  /** That deleted building's name, when it could still be found. */
  removedBuildingName: string | null
  roles: string[]
}

// Search, the building filter and paging all run on the server (ABP's GET /api/identity/users)
// — unlike space types, a company can have thousands of users. Only people who can book are
// listed (Bookings.Create, through any role — admins too while theirs grants it): a building is
// where someone books, so they're the ones who need one. ABP's list doesn't carry building
// names, so each building is fetched once however many users share it.
export function AdminUsersPage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { showToast } = useToast()
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  // Assigning a building goes through ABP's user update: without it the list is read-only.
  const canAssign = usePermission(Permissions.Identity.UsersUpdate)

  const list = useListParams()
  const buildingFilter = list.getFilter('building')
  const roleFilter = list.getFilter('role')

  // Every role name in the system, for the filter — independent of the page/search/filters below.
  const roleNames = useApiQuery(queryKeys.users.roleNames(), () => getRoleNames(token))

  const { status, data, error, isRefreshing, refetch } = useApiQuery(
    queryKeys.users.list({ search: list.search, buildingId: buildingFilter, role: roleFilter, page: list.page, pageSize: list.pageSize }),
    async () => {
      const usersResult = await getUsers(token, {
        filter: list.search || undefined,
        buildingId: buildingFilter || undefined,
        role: roleFilter || undefined,
        permission: Permissions.Bookings.Create,
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

      // One batch call for the whole page's roles, not one per row — a decoration failing
      // shouldn't break the list, so an empty map on error just shows no roles.
      const roles = await getUserRoles(
        token,
        usersResult.items.map((u) => u.id),
      ).catch(() => [])
      const rolesByUserId = new Map(roles.map((r) => [r.userId, r.roles]))

      const rows: UserRow[] = usersResult.items.map((user) => {
        const buildingId = buildingIdOf(user)
        const buildingName = buildingId ? (nameById.get(buildingId) ?? null) : null
        const buildingRemoved = buildingId !== null && !buildingName
        return {
          user,
          buildingId: buildingName ? buildingId : null,
          buildingName,
          buildingRemoved,
          removedBuildingName: buildingRemoved ? (removedNames.get(buildingId) ?? null) : null,
          roles: rolesByUserId.get(user.id) ?? [],
        }
      })
      return { rows, totalCount: usersResult.totalCount }
    },
    { keepPreviousData: true },
  )

  async function handleAssign(userId: string, userLabel: string, buildingId: string) {
    try {
      // Upcoming bookings in the building they're leaving: keep or cancel, before moving them.
      const impact = await getReassignImpact(token, userId)
      if (impact.count > 0 && !(await askImpact({ mode: 'reassign', impact, subject: userLabel }))) return

      await assignUserBuilding(token, userId, buildingId === UNASSIGNED ? null : buildingId)
      // Whole sentences per case (not a "· N cancelled" tail glued on) so each language
      // can word and order them its own way.
      const name = userLabel
      const count = impact.count
      if (buildingId === UNASSIGNED) {
        showToast(count > 0 ? t('Users:UnassignedCancelled', { name, count }) : t('Users:Unassigned', { name }))
      } else {
        showToast(count > 0 ? t('Users:AssignedCancelled', { name, count }) : t('Users:Assigned', { name }))
      }
      refetch()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    }
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">{t('Users:Title')}</h1>
            <p className="lead">
              {status === 'success' ? `${t('Users:CanBook', { count: data.totalCount })} ` : ''}
              {canAssign ? t('Users:LeadAssign') : t('Users:LeadReadOnly')}
            </p>
          </div>

          <Card className="gap-0 rounded-2xl py-0 shadow-md">
            <CardHeader className="flex flex-wrap items-center gap-3 border-b px-5 py-4 [.border-b]:pb-4">
              <CardAction className="flex w-full flex-wrap items-center gap-2 sm:w-auto">
                <div className="relative w-full sm:w-auto">
                  <SearchIcon className="pointer-events-none absolute top-1/2 inset-s-3 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="w-full rounded-full sm:w-64 border-transparent bg-muted/40 ps-9 focus-visible:border-ring focus-visible:bg-background"
                    placeholder={t('Users:Search')}
                    aria-label={t('Users:SearchLabel')}
                    autoComplete="off"
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <BuildingPicker
                  token={token}
                  value={buildingFilter}
                  noneLabel={t('Users:AllBuildings')}
                  ariaLabel={t('Users:FilterBuilding')}
                  onChange={(id) => list.setFilter('building', id)}
                />
                <Select
                  value={roleFilter || ALL_ROLES}
                  onValueChange={(v) => list.setFilter('role', v === ALL_ROLES ? '' : v)}
                >
                  <SelectTrigger className="w-full sm:w-40" aria-label={t('Users:FilterRole')}>
                    <SelectValue placeholder={t('Users:AllRoles')} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_ROLES}>{t('Users:AllRoles')}</SelectItem>
                    {roleNames.data?.map((name) => (
                      <SelectItem key={name} value={name}>
                        {roleLabel(name)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </CardAction>
            </CardHeader>

            <CardContent className={`px-0${isRefreshing ? ' opacity-55 transition-opacity' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <TableSkeleton label={t('Users:Loading')} columns={4} />}
              {status === 'error' && <p className="treeempty">{t('Users:LoadFailed', { error: error.message })}</p>}
              {status === 'success' && data.rows.length === 0 && (
                <p className="treeempty">
                  {list.search || buildingFilter || roleFilter
                    ? t('Users:NoMatch')
                    : t('Users:Empty')}
                </p>
              )}

              {status === 'success' && data.rows.length > 0 && (
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/40 hover:bg-muted/40">
                      <TableHead className="ps-4 font-semibold">{t('Users:ColumnUser')}</TableHead>
                      <TableHead className="hidden font-semibold md:table-cell">{t('Users:ColumnEmail')}</TableHead>
                      <TableHead className="hidden font-semibold lg:table-cell">{t('Users:ColumnRoles')}</TableHead>
                      <TableHead className="pe-4 font-semibold sm:w-60">{t('Users:ColumnBuilding')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.rows.map(({ user, buildingId, buildingName, buildingRemoved, removedBuildingName, roles }) => {
                      const label = labelFor(user)
                      return (
                        <TableRow key={user.id}>
                          <TableCell className="py-3.5 ps-4">
                            <div className="flex items-center gap-3">
                              <Avatar size="lg">
                                <AvatarFallback
                                  className="text-sm font-semibold text-white"
                                  style={{ background: 'var(--gradient-accent)' }}
                                >
                                  {initialsOf(label)}
                                </AvatarFallback>
                              </Avatar>
                              <div>
                                <div className="font-medium">{label}</div>
                                <div className="font-mono text-xs text-muted-foreground">{user.userName}</div>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell className="hidden py-3.5 text-muted-foreground md:table-cell">{user.email || '—'}</TableCell>
                          <TableCell className="hidden py-3.5 lg:table-cell">
                            {roles.length > 0 ? (
                              <div className="flex flex-wrap gap-1.5">
                                {roles.map((role) => (
                                  <Badge key={role} variant={role.toLowerCase() === ADMIN_ROLE ? 'default' : 'secondary'}>
                                    {roleLabel(role)}
                                  </Badge>
                                ))}
                              </div>
                            ) : (
                              <span className="text-sm text-muted-foreground">—</span>
                            )}
                          </TableCell>
                          <TableCell className="py-3.5 pe-4">
                            {canAssign ? (
                              <BuildingPicker
                                token={token}
                                value={buildingId ?? UNASSIGNED}
                                selectedName={buildingName}
                                noneLabel={t('Users:NotAssigned')}
                                ariaLabel={t('Users:BuildingFor', { name: label })}
                                className="w-full sm:w-56"
                                onChange={(id) => handleAssign(user.id, label, id)}
                              />
                            ) : (
                              <span className={buildingName ? undefined : 'text-muted-foreground'}>{buildingName ?? t('Users:NotAssigned')}</span>
                            )}
                            {buildingRemoved && (
                              <p className="mt-1 text-xs text-[var(--state-expired-ink)]">
                                {t(canAssign ? 'Users:RemovedBuildingPick' : 'Users:RemovedBuilding', {
                                  name: removedBuildingName ?? t('Users:DeletedBuilding'),
                                })}
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
    </div>
  )
}
