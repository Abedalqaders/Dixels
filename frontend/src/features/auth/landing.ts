import { HierarchyViewers, Permissions } from '@/features/auth/permissions/permissionNames'
import { satisfies } from '@/features/auth/permissions/usePermission'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

/** Where "home" is: a route that waits for the user's grants, then sends them to landingFor(). */
export const HOME_PATH = '/home'

interface LandingPage {
  permission: PermissionRequirement
  path: string
}

// In order of preference: whoever can run space management lands there (as admins always
// have), everyone else on their own calendar. Picked by grant, not role, so taking a
// permission away never leaves someone landing on a page they can't open.
const LANDING_PAGES: LandingPage[] = [
  { permission: HierarchyViewers.Buildings, path: '/admin/buildings' },
  { permission: Permissions.SpaceTypes.Default, path: '/admin/space-types' },
  { permission: Permissions.Identity.Users, path: '/admin/users' },
  { permission: Permissions.Bookings.Default, path: '/my-calendar' },
  { permission: Permissions.Bookings.Create, path: '/find-space' },
]

/** The first page these grants open, or null when they open none of them. */
export function landingFor(granted: Record<string, boolean>): string | null {
  return LANDING_PAGES.find((page) => satisfies(granted, page.permission))?.path ?? null
}
