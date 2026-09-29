import { Flows, HierarchyViewers, hierarchyPermissions, Permissions } from '@/features/auth/permissions/permissionNames'
import type { HierarchyLevel } from '@/features/auth/permissions/permissionNames'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

/**
 * What each page needs, in one table: App.tsx gates every route with it, and
 * permissionMatrix.test.tsx checks it against the grants each API call needs. Each entry is
 * the permission the page's *first* API call checks, so a user let in never lands on a 403.
 */
export const ROUTE_REQUIREMENTS = {
  '/my-calendar': Permissions.Bookings.Default,
  '/find-space': Flows.FindSpace,
  '/admin/buildings': HierarchyViewers.Buildings,
  '/admin/buildings/:buildingId/floors': HierarchyViewers.Floors,
  '/admin/buildings/:buildingId/floors/:floorId/spaces': HierarchyViewers.Spaces,
  '/admin/space-types': Permissions.SpaceTypes.Default,
  '/admin/users': Permissions.Identity.Users,
} as const satisfies Record<string, PermissionRequirement>

/** The constraints page reads the level it opens on, so it needs that level's own read grant. */
export function constraintsRequirement(level: string | undefined): PermissionRequirement {
  const known = level === 'building' || level === 'floor' || level === 'space'
  return known ? hierarchyPermissions(level as HierarchyLevel).Default : Permissions.Buildings.Default
}
