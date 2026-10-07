import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'
import { appPages } from './modules'

/**
 * What each page needs, in one table, from the module registry: every page's <Gate> is given
 * the same value (see each features/<feature>/module.tsx), and permissionMatrix.test.tsx
 * checks it against the grants each API call needs. Each entry is the permission the page's
 * *first* API call checks, so a user let in never lands on a 403. Pages anyone signed in may
 * open, and the one that picks its permission from the URL, aren't in it.
 */
export const ROUTE_REQUIREMENTS: Record<string, PermissionRequirement> = Object.fromEntries(
  appPages.flatMap((page) =>
    page.permission === null || typeof page.permission === 'function' ? [] : [[page.path, page.permission]],
  ),
)

export { constraintsRequirement } from '@/features/space-management/constraintsRequirement'
