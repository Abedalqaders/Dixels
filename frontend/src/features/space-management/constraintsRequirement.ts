import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'
import { hierarchyPermissions, SpaceManagementPermissions } from './permissions'
import type { HierarchyLevel } from './permissions'

/** The constraints page reads the level it opens on, so it needs that level's own read grant. */
export function constraintsRequirement(level: string | undefined): PermissionRequirement {
  const known = level === 'building' || level === 'floor' || level === 'space'
  return known ? hierarchyPermissions(level as HierarchyLevel).Default : SpaceManagementPermissions.Buildings.Default
}
