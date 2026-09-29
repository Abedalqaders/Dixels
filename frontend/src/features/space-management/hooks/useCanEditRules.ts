import { hierarchyPermissions, Permissions } from '@/features/auth/permissions/permissionNames'
import type { HierarchyLevel } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'

/**
 * Whether the constraints page for `level` opens with something to change, so the menu can
 * say "Edit constraints" rather than "View constraints". Two things can be edited there:
 * the level's own rules (its Edit permission), and its closures — but the closures section
 * is only shown to someone holding Overrides.Default, so Create or Delete without it
 * changes nothing on the page and must not promise an edit.
 */
export function useCanEditRules(level: HierarchyLevel): boolean {
  const canEditLevel = usePermission(hierarchyPermissions(level).Edit)
  const canSeeClosures = usePermission(Permissions.Overrides.Default)
  const canChangeClosures = usePermission([Permissions.Overrides.Create, Permissions.Overrides.Delete])
  return canEditLevel || (canSeeClosures && canChangeClosures)
}
